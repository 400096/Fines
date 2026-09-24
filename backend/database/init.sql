/*
  考核管理系统 - MySQL 8.0+ 初始化脚本
  默认管理员：admin / Admin123!
  首次登录后请立即修改密码，并修改 appsettings.json 中的 JWT 密钥。
*/

CREATE DATABASE IF NOT EXISTS `fine_management`
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_0900_ai_ci;

USE `fine_management`;

CREATE TABLE IF NOT EXISTS `Departments`
(
    `Id`        BIGINT NOT NULL AUTO_INCREMENT COMMENT '部门主键',
    `Name`      VARCHAR(100) NOT NULL COMMENT '部门名称',
    `Code`      VARCHAR(30) NOT NULL COMMENT '部门编码',
    `SortOrder` INT NOT NULL DEFAULT 0 COMMENT '显示排序，数值越小越靠前',
    `IsEnabled` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否启用：1启用，0停用',
    `Manager`   VARCHAR(50) NULL COMMENT '部门负责人',
    `Remark`    VARCHAR(300) NULL COMMENT '备注',
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) COMMENT '创建时间（UTC）',
    `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) COMMENT '最后更新时间（UTC）',
    `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '逻辑删除标记：1已删除，0正常',
    `DeletedAt` DATETIME(6) NULL COMMENT '逻辑删除时间（UTC）',
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_Departments_Name` (`Name`),
    UNIQUE KEY `UQ_Departments_Code` (`Code`),
    KEY `IX_Departments_IsDeleted` (`IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='部门信息表';

CREATE TABLE IF NOT EXISTS `PaymentMethods`
(
    `Id`          BIGINT NOT NULL AUTO_INCREMENT COMMENT '收款方式主键',
    `Name`        VARCHAR(50) NOT NULL COMMENT '收款方式名称',
    `SortOrder`   INT NOT NULL DEFAULT 0 COMMENT '显示排序，数值越小越靠前',
    `IsEnabled`   TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否启用：1启用，0停用',
    `Description` VARCHAR(300) NULL COMMENT '收款方式说明',
    `CreatedAt`   DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) COMMENT '创建时间（UTC）',
    `UpdatedAt`   DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) COMMENT '最后更新时间（UTC）',
    `IsDeleted`   TINYINT(1) NOT NULL DEFAULT 0 COMMENT '逻辑删除标记：1已删除，0正常',
    `DeletedAt`   DATETIME(6) NULL COMMENT '逻辑删除时间（UTC）',
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_PaymentMethods_Name` (`Name`),
    KEY `IX_PaymentMethods_IsDeleted` (`IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='收款方式表';

CREATE TABLE IF NOT EXISTS `Users`
(
    `Id`           BIGINT NOT NULL AUTO_INCREMENT COMMENT '用户主键',
    `Username`     VARCHAR(50) NOT NULL COMMENT '登录用户名',
    `PasswordHash` VARCHAR(500) NOT NULL COMMENT 'ASP.NET Identity 密码哈希',
    `Name`         VARCHAR(50) NOT NULL COMMENT '用户姓名',
    `Phone`        VARCHAR(30) NULL COMMENT '联系电话',
    `Role`         VARCHAR(50) NOT NULL COMMENT '系统角色',
    `IsEnabled`    TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否允许登录：1启用，0停用',
    `LastLoginAt`  DATETIME(6) NULL COMMENT '最后登录时间（UTC）',
    `CreatedAt`    DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) COMMENT '创建时间（UTC）',
    `UpdatedAt`    DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) COMMENT '最后更新时间（UTC）',
    `IsDeleted`    TINYINT(1) NOT NULL DEFAULT 0 COMMENT '逻辑删除标记：1已删除，0正常',
    `DeletedAt`    DATETIME(6) NULL COMMENT '逻辑删除时间（UTC）',
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_Users_Username` (`Username`),
    KEY `IX_Users_IsDeleted` (`IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='系统用户表';

CREATE TABLE IF NOT EXISTS `AssessmentRecords`
(
    `Id`                 BIGINT NOT NULL AUTO_INCREMENT COMMENT '考核记录主键',
    `AssessmentDateType` VARCHAR(20) NOT NULL DEFAULT 'Fixed' COMMENT '单据日期类型：Fixed固定日期，Range日期区间',
    `AssessmentDate`     DATE NOT NULL COMMENT '考核开始日期或固定日期',
    `AssessmentEndDate`  DATE NULL COMMENT '考核结束日期，仅日期区间类型使用',
    `DetailDate`         DATE NOT NULL COMMENT '考核明细到达财务并登记的日期',
    `DepartmentId`       BIGINT NOT NULL COMMENT '所属部门主键',
    `PersonName`         VARCHAR(50) NOT NULL COMMENT '被考核人员姓名，允许多个姓名',
    `Amount`             DECIMAL(18,2) NOT NULL COMMENT '考核总金额',
    `Reason`             VARCHAR(300) NOT NULL COMMENT '考核事由',
    `Remark`             VARCHAR(500) NULL COMMENT '备注',
    `Status`             VARCHAR(20) NOT NULL DEFAULT 'Pending' COMMENT '处理状态：Pending待处理，Completed已完成',
    `PaymentMethodId`    BIGINT NULL COMMENT '收款方式主键，设置后视为已完成',
    `CreatedByUserId`    BIGINT NULL COMMENT '创建记录的用户主键',
    `CreatedAt`          DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) COMMENT '创建时间（UTC）',
    `UpdatedAt`          DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) COMMENT '最后更新时间（UTC）',
    `IsDeleted`          TINYINT(1) NOT NULL DEFAULT 0 COMMENT '逻辑删除标记：1已删除，0正常',
    `DeletedAt`          DATETIME(6) NULL COMMENT '逻辑删除时间（UTC）',
    PRIMARY KEY (`Id`),
    CONSTRAINT `CK_AssessmentRecords_Amount` CHECK (`Amount` > 0),
    CONSTRAINT `CK_AssessmentRecords_Status` CHECK (`Status` IN ('Pending', 'Completed')),
    CONSTRAINT `FK_AssessmentRecords_Departments`
        FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_AssessmentRecords_PaymentMethods`
        FOREIGN KEY (`PaymentMethodId`) REFERENCES `PaymentMethods` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_AssessmentRecords_Users`
        FOREIGN KEY (`CreatedByUserId`) REFERENCES `Users` (`Id`) ON DELETE SET NULL,
    KEY `IX_AssessmentRecords_AssessmentDate` (`AssessmentDate`),
    KEY `IX_AssessmentRecords_DetailDate` (`DetailDate`),
    KEY `IX_AssessmentRecords_DepartmentId_AssessmentDate` (`DepartmentId`, `AssessmentDate`),
    KEY `IX_AssessmentRecords_Status` (`Status`),
    KEY `IX_AssessmentRecords_IsDeleted` (`IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='正式考核记录表';

CREATE TABLE IF NOT EXISTS `AssessmentImportBatches`
(
    `Id`              BIGINT NOT NULL AUTO_INCREMENT COMMENT '导入批次主键',
    `Status`          VARCHAR(20) NOT NULL DEFAULT 'Pending' COMMENT '批次状态：Pending待确认，Confirmed已确认，Cancelled已取消',
    `CreatedByUserId` BIGINT NULL COMMENT '发起导入的用户主键',
    `CreatedAt`       DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) COMMENT '创建时间（UTC）',
    `UpdatedAt`       DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) COMMENT '最后更新时间（UTC）',
    `ConfirmedAt`     DATETIME(6) NULL COMMENT '确认导入时间（UTC）',
    `IsDeleted`       TINYINT(1) NOT NULL DEFAULT 0 COMMENT '逻辑删除标记：1已删除，0正常',
    `DeletedAt`       DATETIME(6) NULL COMMENT '逻辑删除时间（UTC）',
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ImportBatches_Users`
        FOREIGN KEY (`CreatedByUserId`) REFERENCES `Users` (`Id`) ON DELETE SET NULL,
    KEY `IX_AssessmentImportBatches_Status` (`Status`),
    KEY `IX_AssessmentImportBatches_IsDeleted` (`IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Excel 导入批次表';

CREATE TABLE IF NOT EXISTS `AssessmentImportRows`
(
    `Id`                 BIGINT NOT NULL AUTO_INCREMENT COMMENT '暂存明细主键',
    `BatchId`            BIGINT NOT NULL COMMENT '所属导入批次主键',
    `SourceRowNumber`    INT NOT NULL DEFAULT 0 COMMENT '原 Excel 行号，手工添加时为0',
    `AssessmentDateType` VARCHAR(20) NOT NULL DEFAULT 'Fixed' COMMENT '单据日期类型：Fixed固定日期，Range日期区间',
    `AssessmentDate`     DATE NOT NULL COMMENT '考核开始日期或固定日期',
    `AssessmentEndDate`  DATE NULL COMMENT '考核结束日期，仅日期区间类型使用',
    `DetailDate`         DATE NOT NULL COMMENT '当前明细分组对应的录入日期',
    `DepartmentId`       BIGINT NULL COMMENT '已匹配的部门主键，未识别时为空',
    `DepartmentName`     VARCHAR(100) NOT NULL COMMENT 'Excel 中识别出的部门名称',
    `PersonName`         VARCHAR(50) NOT NULL COMMENT '被考核人员姓名，允许多个姓名',
    `Amount`             DECIMAL(18,2) NOT NULL COMMENT '考核总金额',
    `Reason`             VARCHAR(300) NOT NULL COMMENT '考核事由',
    `Remark`             VARCHAR(500) NULL COMMENT '备注',
    `PaymentMethodId`    BIGINT NULL COMMENT '已匹配的收款方式主键',
    `CreatedAt`          DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) COMMENT '创建时间（UTC）',
    `UpdatedAt`          DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) COMMENT '最后更新时间（UTC）',
    `IsDeleted`          TINYINT(1) NOT NULL DEFAULT 0 COMMENT '逻辑删除标记：1已删除，0正常',
    `DeletedAt`          DATETIME(6) NULL COMMENT '逻辑删除时间（UTC）',
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ImportRows_Batches`
        FOREIGN KEY (`BatchId`) REFERENCES `AssessmentImportBatches` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ImportRows_Departments`
        FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ImportRows_PaymentMethods`
        FOREIGN KEY (`PaymentMethodId`) REFERENCES `PaymentMethods` (`Id`) ON DELETE RESTRICT,
    KEY `IX_AssessmentImportRows_BatchId_IsDeleted` (`BatchId`, `IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Excel 导入待确认明细表';

INSERT IGNORE INTO `PaymentMethods`
    (`Name`, `SortOrder`, `IsEnabled`, `Description`)
VALUES
    ('微信', 1, 1, '通过微信收款'),
    ('现金', 2, 1, '现金收取'),
    ('支付宝', 3, 1, '通过支付宝收款'),
    ('银行转账', 4, 1, '对公账户入账银行转账'),
    ('工资扣除', 5, 1, '从当月工资中扣除'),
    ('其他', 6, 1, '其他方式收款');

INSERT IGNORE INTO `Users`
    (`Username`, `PasswordHash`, `Name`, `Phone`, `Role`, `IsEnabled`)
VALUES
    (
        'admin',
        'AQAAAAIAAYagAAAAEMVl5+s8T58wK0CkHXOazS3jFV6S3RJJJ+lydQ0I8xEvxt9L7icpedKqpsSgGnZ9og==',
        '管理员',
        '17863654727',
        '超级管理员',
        1
    );
