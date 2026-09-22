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
    `Id`        BIGINT NOT NULL AUTO_INCREMENT,
    `Name`      VARCHAR(100) NOT NULL,
    `Code`      VARCHAR(30) NOT NULL,
    `SortOrder` INT NOT NULL DEFAULT 0,
    `IsEnabled` TINYINT(1) NOT NULL DEFAULT 1,
    `Manager`   VARCHAR(50) NULL,
    `Remark`    VARCHAR(300) NULL,
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0,
    `DeletedAt` DATETIME(6) NULL,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_Departments_Name` (`Name`),
    UNIQUE KEY `UQ_Departments_Code` (`Code`),
    KEY `IX_Departments_IsDeleted` (`IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `PaymentMethods`
(
    `Id`          BIGINT NOT NULL AUTO_INCREMENT,
    `Name`        VARCHAR(50) NOT NULL,
    `SortOrder`   INT NOT NULL DEFAULT 0,
    `IsEnabled`   TINYINT(1) NOT NULL DEFAULT 1,
    `Description` VARCHAR(300) NULL,
    `CreatedAt`   DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt`   DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `IsDeleted`   TINYINT(1) NOT NULL DEFAULT 0,
    `DeletedAt`   DATETIME(6) NULL,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_PaymentMethods_Name` (`Name`),
    KEY `IX_PaymentMethods_IsDeleted` (`IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `Users`
(
    `Id`           BIGINT NOT NULL AUTO_INCREMENT,
    `Username`     VARCHAR(50) NOT NULL,
    `PasswordHash` VARCHAR(500) NOT NULL,
    `Name`         VARCHAR(50) NOT NULL,
    `Phone`        VARCHAR(30) NULL,
    `Role`         VARCHAR(50) NOT NULL,
    `IsEnabled`    TINYINT(1) NOT NULL DEFAULT 1,
    `LastLoginAt`  DATETIME(6) NULL,
    `CreatedAt`    DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt`    DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `IsDeleted`    TINYINT(1) NOT NULL DEFAULT 0,
    `DeletedAt`    DATETIME(6) NULL,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_Users_Username` (`Username`),
    KEY `IX_Users_IsDeleted` (`IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `AssessmentRecords`
(
    `Id`              BIGINT NOT NULL AUTO_INCREMENT,
    `AssessmentDate`  DATE NOT NULL,
    `DepartmentId`    BIGINT NOT NULL,
    `PersonName`      VARCHAR(50) NOT NULL,
    `Amount`          DECIMAL(18,2) NOT NULL,
    `Reason`          VARCHAR(300) NOT NULL,
    `Remark`          VARCHAR(500) NULL,
    `Status`          VARCHAR(20) NOT NULL DEFAULT 'Pending',
    `PaymentMethodId` BIGINT NULL,
    `CreatedByUserId` BIGINT NULL,
    `CreatedAt`       DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt`       DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `IsDeleted`       TINYINT(1) NOT NULL DEFAULT 0,
    `DeletedAt`       DATETIME(6) NULL,
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
    KEY `IX_AssessmentRecords_DepartmentId_AssessmentDate` (`DepartmentId`, `AssessmentDate`),
    KEY `IX_AssessmentRecords_Status` (`Status`),
    KEY `IX_AssessmentRecords_IsDeleted` (`IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `AssessmentImportBatches`
(
    `Id`              BIGINT NOT NULL AUTO_INCREMENT,
    `Status`          VARCHAR(20) NOT NULL DEFAULT 'Pending',
    `CreatedByUserId` BIGINT NULL,
    `CreatedAt`       DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt`       DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `ConfirmedAt`     DATETIME(6) NULL,
    `IsDeleted`       TINYINT(1) NOT NULL DEFAULT 0,
    `DeletedAt`       DATETIME(6) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ImportBatches_Users`
        FOREIGN KEY (`CreatedByUserId`) REFERENCES `Users` (`Id`) ON DELETE SET NULL,
    KEY `IX_AssessmentImportBatches_Status` (`Status`),
    KEY `IX_AssessmentImportBatches_IsDeleted` (`IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `AssessmentImportRows`
(
    `Id`               BIGINT NOT NULL AUTO_INCREMENT,
    `BatchId`          BIGINT NOT NULL,
    `SourceRowNumber`  INT NOT NULL DEFAULT 0,
    `AssessmentDate`   DATE NOT NULL,
    `DepartmentId`     BIGINT NULL,
    `DepartmentName`   VARCHAR(100) NOT NULL,
    `PersonName`       VARCHAR(50) NOT NULL,
    `Amount`           DECIMAL(18,2) NOT NULL,
    `Reason`           VARCHAR(300) NOT NULL,
    `Remark`           VARCHAR(500) NULL,
    `PaymentMethodId`  BIGINT NULL,
    `CreatedAt`        DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt`        DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `IsDeleted`        TINYINT(1) NOT NULL DEFAULT 0,
    `DeletedAt`        DATETIME(6) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ImportRows_Batches`
        FOREIGN KEY (`BatchId`) REFERENCES `AssessmentImportBatches` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ImportRows_Departments`
        FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ImportRows_PaymentMethods`
        FOREIGN KEY (`PaymentMethodId`) REFERENCES `PaymentMethods` (`Id`) ON DELETE RESTRICT,
    KEY `IX_AssessmentImportRows_BatchId_IsDeleted` (`BatchId`, `IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

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
