using FineManagement.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FineManagement.Api.Data;

/// <summary>
/// 首次启动时创建基础数据，并对旧版本数据库执行幂等的字段、索引兼容升级。
/// </summary>
public static class DbInitializer
{
    /// <summary>初始化数据库结构、默认收款方式和初始管理员。</summary>
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await EnsureAssessmentChangeSchemaAsync(db);
        await EnsureImportStagingSchemaAsync(db);
        await EnsureAssessmentDateSchemaAsync(db);
        await EnsureSoftDeleteSchemaAsync(db);

        var now = DateTime.UtcNow;
        if (!await db.PaymentMethods.IgnoreQueryFilters().AnyAsync())
        {
            db.PaymentMethods.AddRange(
                new PaymentMethod { Name = "微信", SortOrder = 1, Description = "通过微信收款", CreatedAt = now, UpdatedAt = now },
                new PaymentMethod { Name = "现金", SortOrder = 2, Description = "现金收取", CreatedAt = now, UpdatedAt = now },
                new PaymentMethod { Name = "支付宝", SortOrder = 3, Description = "通过支付宝收款", CreatedAt = now, UpdatedAt = now },
                new PaymentMethod { Name = "银行转账", SortOrder = 4, Description = "对公账户入账银行转账", CreatedAt = now, UpdatedAt = now },
                new PaymentMethod { Name = "工资扣除", SortOrder = 5, Description = "从当月工资中扣除", CreatedAt = now, UpdatedAt = now },
                new PaymentMethod { Name = "其他", SortOrder = 6, Description = "其他方式收款", CreatedAt = now, UpdatedAt = now });
        }

        if (!await db.Users.IgnoreQueryFilters().AnyAsync())
        {
            var admin = new AppUser
            {
                Username = "admin",
                Name = "管理员",
                Phone = "17863654727",
                Role = "超级管理员",
                IsEnabled = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            admin.PasswordHash = new PasswordHasher<AppUser>().HashPassword(admin, "Admin123!");
            db.Users.Add(admin);
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureAssessmentChangeSchemaAsync(AppDbContext db)
    {
        // EnsureCreated 不会为已有数据库补建新表，启动时幂等创建操作历史表。
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `AssessmentRecordChanges`
            (
                `Id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '变更日志主键',
                `AssessmentRecordId` BIGINT NOT NULL COMMENT '所属考核记录主键',
                `Action` VARCHAR(20) NOT NULL COMMENT '操作类型：Created、Updated、Imported、Deleted',
                `ActorUserId` BIGINT NULL COMMENT '操作用户主键快照',
                `ActorName` VARCHAR(100) NOT NULL COMMENT '操作人显示名称快照',
                `OccurredAt` DATETIME(6) NOT NULL COMMENT '操作时间（UTC）',
                `ChangesJson` LONGTEXT NOT NULL COMMENT '字段变更明细 JSON',
                PRIMARY KEY (`Id`),
                CONSTRAINT `FK_AssessmentRecordChanges_Records` FOREIGN KEY (`AssessmentRecordId`)
                    REFERENCES `AssessmentRecords` (`Id`) ON DELETE RESTRICT,
                KEY `IX_AssessmentRecordChanges_AssessmentRecordId_OccurredAt` (`AssessmentRecordId`, `OccurredAt`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='考核记录操作及字段变更历史表'
            """);
    }

    private static async Task EnsureSoftDeleteSchemaAsync(AppDbContext db)
    {
        // 老数据库可能没有逻辑删除字段；逐表检查后再添加，避免重复执行时报错。
        var tables = new[]
        {
            "AssessmentRecords", "AssessmentImportBatches", "AssessmentImportRows",
            "Departments", "PaymentMethods", "Users"
        };
        await db.Database.OpenConnectionAsync();
        try
        {
            var connection = db.Database.GetDbConnection();
            foreach (var table in tables)
            {
                if (!await ColumnExistsAsync(connection, table, "IsDeleted"))
                {
                    await using var addDeletedFlag = connection.CreateCommand();
                    addDeletedFlag.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '逻辑删除标记：1已删除，0正常'";
                    await addDeletedFlag.ExecuteNonQueryAsync();
                }

                if (!await ColumnExistsAsync(connection, table, "DeletedAt"))
                {
                    await using var addDeletedAt = connection.CreateCommand();
                    addDeletedAt.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `DeletedAt` DATETIME(6) NULL COMMENT '逻辑删除时间（UTC）'";
                    await addDeletedAt.ExecuteNonQueryAsync();
                }

                if (!await IndexExistsAsync(connection, table, $"IX_{table}_IsDeleted"))
                {
                    await using var addIndex = connection.CreateCommand();
                    addIndex.CommandText = $"CREATE INDEX `IX_{table}_IsDeleted` ON `{table}` (`IsDeleted`)";
                    await addIndex.ExecuteNonQueryAsync();
                }
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureImportStagingSchemaAsync(AppDbContext db)
    {
        // 导入数据先进入批次和暂存明细表，只有确认后才写入正式考核记录。
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `AssessmentImportBatches`
            (
                `Id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '导入批次主键',
                `Status` VARCHAR(20) NOT NULL DEFAULT 'Pending' COMMENT '批次状态',
                `CreatedByUserId` BIGINT NULL COMMENT '发起导入的用户主键',
                `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC）',
                `UpdatedAt` DATETIME(6) NOT NULL COMMENT '最后更新时间（UTC）',
                `ConfirmedAt` DATETIME(6) NULL COMMENT '确认导入时间（UTC）',
                `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '逻辑删除标记',
                `DeletedAt` DATETIME(6) NULL COMMENT '逻辑删除时间（UTC）',
                PRIMARY KEY (`Id`),
                CONSTRAINT `FK_ImportBatches_Users` FOREIGN KEY (`CreatedByUserId`)
                    REFERENCES `Users` (`Id`) ON DELETE SET NULL,
                KEY `IX_AssessmentImportBatches_Status` (`Status`),
                KEY `IX_AssessmentImportBatches_IsDeleted` (`IsDeleted`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Excel 导入批次表'
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `AssessmentImportRows`
            (
                `Id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '暂存明细主键',
                `BatchId` BIGINT NOT NULL COMMENT '所属导入批次主键',
                `SourceRowNumber` INT NOT NULL DEFAULT 0 COMMENT '原 Excel 行号，手工添加时为0',
                `AssessmentDateType` VARCHAR(20) NOT NULL DEFAULT 'Fixed' COMMENT '单据日期类型',
                `AssessmentDate` DATE NOT NULL COMMENT '考核开始日期或固定日期',
                `AssessmentEndDate` DATE NULL COMMENT '考核结束日期',
                `DetailDate` DATE NOT NULL COMMENT '录入日期',
                `DepartmentId` BIGINT NULL COMMENT '已匹配的部门主键',
                `DepartmentName` VARCHAR(100) NOT NULL COMMENT 'Excel 中识别出的部门名称',
                `PersonName` VARCHAR(50) NOT NULL COMMENT '被考核人员姓名',
                `Amount` DECIMAL(18,2) NOT NULL COMMENT '考核总金额',
                `Reason` VARCHAR(300) NOT NULL COMMENT '考核事由',
                `Remark` VARCHAR(500) NULL COMMENT '备注',
                `PaymentMethodId` BIGINT NULL COMMENT '已匹配的收款方式主键',
                `CreatedAt` DATETIME(6) NOT NULL COMMENT '创建时间（UTC）',
                `UpdatedAt` DATETIME(6) NOT NULL COMMENT '最后更新时间（UTC）',
                `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '逻辑删除标记',
                `DeletedAt` DATETIME(6) NULL COMMENT '逻辑删除时间（UTC）',
                PRIMARY KEY (`Id`),
                CONSTRAINT `FK_ImportRows_Batches` FOREIGN KEY (`BatchId`)
                    REFERENCES `AssessmentImportBatches` (`Id`) ON DELETE RESTRICT,
                CONSTRAINT `FK_ImportRows_Departments` FOREIGN KEY (`DepartmentId`)
                    REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
                CONSTRAINT `FK_ImportRows_PaymentMethods` FOREIGN KEY (`PaymentMethodId`)
                    REFERENCES `PaymentMethods` (`Id`) ON DELETE RESTRICT,
                KEY `IX_AssessmentImportRows_BatchId_IsDeleted` (`BatchId`, `IsDeleted`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Excel 导入待确认明细表'
            """);
    }

    private static async Task EnsureAssessmentDateSchemaAsync(AppDbContext db)
    {
        // 兼容历史库：补充日期类型、结束日期和财务登记的录入日期。
        var tables = new[] { "AssessmentRecords", "AssessmentImportRows" };
        await db.Database.OpenConnectionAsync();
        try
        {
            var connection = db.Database.GetDbConnection();
            foreach (var table in tables)
            {
                if (!await ColumnExistsAsync(connection, table, "AssessmentDateType"))
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `AssessmentDateType` VARCHAR(20) NOT NULL DEFAULT 'Fixed' COMMENT '单据日期类型：Fixed固定日期，Range日期区间' AFTER `AssessmentDate`";
                    await command.ExecuteNonQueryAsync();
                }

                if (!await ColumnExistsAsync(connection, table, "AssessmentEndDate"))
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `AssessmentEndDate` DATE NULL COMMENT '考核结束日期，仅日期区间类型使用' AFTER `AssessmentDate`";
                    await command.ExecuteNonQueryAsync();
                }

                if (!await ColumnExistsAsync(connection, table, "DetailDate"))
                {
                    await using var addColumn = connection.CreateCommand();
                    addColumn.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `DetailDate` DATE NULL COMMENT '考核明细到达财务并登记的日期' AFTER `AssessmentEndDate`";
                    await addColumn.ExecuteNonQueryAsync();

                    await using var backfill = connection.CreateCommand();
                    backfill.CommandText = $"UPDATE `{table}` SET `DetailDate` = `AssessmentDate` WHERE `DetailDate` IS NULL";
                    await backfill.ExecuteNonQueryAsync();

                    await using var makeRequired = connection.CreateCommand();
                    makeRequired.CommandText = $"ALTER TABLE `{table}` MODIFY COLUMN `DetailDate` DATE NOT NULL COMMENT '考核明细到达财务并登记的日期'";
                    await makeRequired.ExecuteNonQueryAsync();
                }
            }

            if (!await IndexExistsAsync(connection, "AssessmentRecords", "IX_AssessmentRecords_DetailDate"))
            {
                await using var addIndex = connection.CreateCommand();
                addIndex.CommandText = "CREATE INDEX `IX_AssessmentRecords_DetailDate` ON `AssessmentRecords` (`DetailDate`)";
                await addIndex.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<bool> ColumnExistsAsync(System.Data.Common.DbConnection connection, string table, string column)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND COLUMN_NAME = @column";
        AddParameter(command, "@table", table);
        AddParameter(command, "@column", column);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static async Task<bool> IndexExistsAsync(System.Data.Common.DbConnection connection, string table, string index)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND INDEX_NAME = @index";
        AddParameter(command, "@table", table);
        AddParameter(command, "@index", index);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
