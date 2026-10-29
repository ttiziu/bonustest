/* Ejecutar después de 01_schema_seed.sql. Reejecutable sin borrar datos. */
USE BonusTest;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.Bonus', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Bonus (
        BonusId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Bonus PRIMARY KEY,
        DepartmentCode CHAR(5) NOT NULL,
        EmployeeNumber INT NOT NULL,
        Period DATE NOT NULL,
        BonusTypeId INT NOT NULL,
        Value DECIMAL(18,4) NOT NULL,
        ReasonCode CHAR(6) NOT NULL,
        StateCode CHAR(4) NOT NULL CONSTRAINT DF_Bonus_State DEFAULT '0201',
        MonthlySalary DECIMAL(16,2) NOT NULL,
        TaxRate DECIMAL(5,2) NOT NULL,
        GrossAmount DECIMAL(18,2) NOT NULL,
        TaxAmount DECIMAL(18,2) NOT NULL,
        NetAmount DECIMAL(18,2) NOT NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_Bonus_IsDeleted DEFAULT 0,
        CreatedBy NVARCHAR(100) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        ModifiedBy NVARCHAR(100) NOT NULL,
        ModifiedAt DATETIMEOFFSET NOT NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_Bonus_Employee FOREIGN KEY (DepartmentCode, EmployeeNumber)
            REFERENCES dbo.Employee(DepartmentCode, EmployeeNumber),
        CONSTRAINT FK_Bonus_Department FOREIGN KEY (DepartmentCode) REFERENCES dbo.Department(DepartmentCode),
        CONSTRAINT FK_Bonus_Type FOREIGN KEY (BonusTypeId) REFERENCES dbo.BonusType(BonusTypeId),
        CONSTRAINT FK_Bonus_Reason FOREIGN KEY (ReasonCode) REFERENCES dbo.BonusReason(ReasonCode),
        CONSTRAINT FK_Bonus_State FOREIGN KEY (StateCode) REFERENCES dbo.State(StateCode),
        CONSTRAINT CK_Bonus_Period CHECK (DAY(Period) = 1),
        CONSTRAINT CK_Bonus_State CHECK (StateCode IN ('0201', '0202', '0203')),
        CONSTRAINT CK_Bonus_Value CHECK (Value > 0 AND
            ((BonusTypeId = 1 AND Value <= MonthlySalary) OR (BonusTypeId = 2 AND Value <= 100))),
        CONSTRAINT CK_Bonus_Amounts CHECK (MonthlySalary > 0 AND TaxRate BETWEEN 0 AND 100 AND
            GrossAmount >= 0 AND TaxAmount >= 0 AND NetAmount >= 0 AND NetAmount = GrossAmount - TaxAmount)
    );
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Bonus') AND name = 'UX_Bonus_ActiveEmployeePeriod')
BEGIN
    -- Garantiza unicidad incluso con solicitudes simultáneas; anular/eliminar libera el periodo.
    CREATE UNIQUE INDEX UX_Bonus_ActiveEmployeePeriod
        ON dbo.Bonus(DepartmentCode, EmployeeNumber, Period)
        WHERE IsDeleted = 0 AND StateCode <> '0203';
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Bonus') AND name = 'IX_Bonus_List')
BEGIN
    -- Apoya listado por periodo/estado y orden estable, excluyendo eliminados.
    CREATE INDEX IX_Bonus_List ON dbo.Bonus(Period, StateCode, CreatedAt DESC, BonusId DESC)
        INCLUDE (DepartmentCode, EmployeeNumber) WHERE IsDeleted = 0;
END;
COMMIT;
GO
