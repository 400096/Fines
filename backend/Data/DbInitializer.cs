using FineManagement.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FineManagement.Api.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await EnsureImportStagingSchemaAsync(db);
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

    private static async Task EnsureSoftDeleteSchemaAsync(AppDbContext db)
    {
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
                    addDeletedFlag.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0";
                    await addDeletedFlag.ExecuteNonQueryAsync();
                }

                if (!await ColumnExistsAsync(connection, table, "DeletedAt"))
                {
                    await using var addDeletedAt = connection.CreateCommand();
                    addDeletedAt.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `DeletedAt` DATETIME(6) NULL";
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
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `AssessmentImportBatches`
            (
                `Id` BIGINT NOT NULL AUTO_INCREMENT,
                `Status` VARCHAR(20) NOT NULL DEFAULT 'Pending',
                `CreatedByUserId` BIGINT NULL,
                `CreatedAt` DATETIME(6) NOT NULL,
                `UpdatedAt` DATETIME(6) NOT NULL,
                `ConfirmedAt` DATETIME(6) NULL,
                `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0,
                `DeletedAt` DATETIME(6) NULL,
                PRIMARY KEY (`Id`),
                CONSTRAINT `FK_ImportBatches_Users` FOREIGN KEY (`CreatedByUserId`)
                    REFERENCES `Users` (`Id`) ON DELETE SET NULL,
                KEY `IX_AssessmentImportBatches_Status` (`Status`),
                KEY `IX_AssessmentImportBatches_IsDeleted` (`IsDeleted`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `AssessmentImportRows`
            (
                `Id` BIGINT NOT NULL AUTO_INCREMENT,
                `BatchId` BIGINT NOT NULL,
                `SourceRowNumber` INT NOT NULL DEFAULT 0,
                `AssessmentDate` DATE NOT NULL,
                `DepartmentId` BIGINT NULL,
                `DepartmentName` VARCHAR(100) NOT NULL,
                `PersonName` VARCHAR(50) NOT NULL,
                `Amount` DECIMAL(18,2) NOT NULL,
                `Reason` VARCHAR(300) NOT NULL,
                `Remark` VARCHAR(500) NULL,
                `PaymentMethodId` BIGINT NULL,
                `CreatedAt` DATETIME(6) NOT NULL,
                `UpdatedAt` DATETIME(6) NOT NULL,
                `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0,
                `DeletedAt` DATETIME(6) NULL,
                PRIMARY KEY (`Id`),
                CONSTRAINT `FK_ImportRows_Batches` FOREIGN KEY (`BatchId`)
                    REFERENCES `AssessmentImportBatches` (`Id`) ON DELETE RESTRICT,
                CONSTRAINT `FK_ImportRows_Departments` FOREIGN KEY (`DepartmentId`)
                    REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
                CONSTRAINT `FK_ImportRows_PaymentMethods` FOREIGN KEY (`PaymentMethodId`)
                    REFERENCES `PaymentMethods` (`Id`) ON DELETE RESTRICT,
                KEY `IX_AssessmentImportRows_BatchId_IsDeleted` (`BatchId`, `IsDeleted`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
            """);
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
