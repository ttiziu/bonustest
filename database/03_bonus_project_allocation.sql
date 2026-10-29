/* Punto extra: detalle del reparto del bruto. Ejecutar después de 02_bonus.sql. */
USE BonusTest;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.BonusProjectAllocation', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BonusProjectAllocation (
        BonusProjectAllocationId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BonusProjectAllocation PRIMARY KEY,
        BonusId INT NOT NULL,
        EmployeeProjectHoursId INT NOT NULL,
        ProjectName VARCHAR(100) NOT NULL,
        Hours DECIMAL(8,2) NOT NULL,
        GrossAmount DECIMAL(18,2) NOT NULL,
        CONSTRAINT FK_BonusProjectAllocation_Bonus FOREIGN KEY (BonusId) REFERENCES dbo.Bonus(BonusId),
        CONSTRAINT FK_BonusProjectAllocation_Hours FOREIGN KEY (EmployeeProjectHoursId)
            REFERENCES dbo.EmployeeProjectHours(EmployeeProjectHoursId),
        CONSTRAINT CK_BonusProjectAllocation_Hours CHECK (Hours >= 0),
        CONSTRAINT CK_BonusProjectAllocation_Amount CHECK (GrossAmount >= 0),
        CONSTRAINT UQ_BonusProjectAllocation_Project UNIQUE (BonusId, EmployeeProjectHoursId)
    );
END;
COMMIT;
GO
