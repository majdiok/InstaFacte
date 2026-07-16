-- Idempotent tenant migration: AddPayrollRegimeSectorFamily_Tenant
-- Paie : régime hebdomadaire du contrat (HS), secteur TFP par exercice et
-- déductions familiales étendues (art. 40 code IRPP). Tous les défauts
-- préservent le comportement antérieur (48 h, secteur non industriel, compteurs à 0).

IF OBJECT_ID(N'dbo.EmploymentContracts', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.EmploymentContracts', N'WeeklyRegime') IS NULL
    BEGIN
        ALTER TABLE dbo.EmploymentContracts ADD WeeklyRegime int NOT NULL CONSTRAINT DF_EmploymentContracts_WeeklyRegime DEFAULT 0;
    END
END

IF OBJECT_ID(N'dbo.PayrollYearParameters', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.PayrollYearParameters', N'IsIndustrialSector') IS NULL
    BEGIN
        ALTER TABLE dbo.PayrollYearParameters ADD IsIndustrialSector bit NOT NULL CONSTRAINT DF_PayrollYearParameters_IsIndustrialSector DEFAULT 0;
    END

    IF COL_LENGTH(N'dbo.PayrollYearParameters', N'StudentChildAnnualDeduction') IS NULL
    BEGIN
        ALTER TABLE dbo.PayrollYearParameters ADD StudentChildAnnualDeduction decimal(18,3) NOT NULL CONSTRAINT DF_PayrollYearParameters_StudentChildAnnualDeduction DEFAULT 1000;
    END

    IF COL_LENGTH(N'dbo.PayrollYearParameters', N'DisabledChildAnnualDeduction') IS NULL
    BEGIN
        ALTER TABLE dbo.PayrollYearParameters ADD DisabledChildAnnualDeduction decimal(18,3) NOT NULL CONSTRAINT DF_PayrollYearParameters_DisabledChildAnnualDeduction DEFAULT 2000;
    END

    IF COL_LENGTH(N'dbo.PayrollYearParameters', N'ParentDeductionRatePercent') IS NULL
    BEGIN
        ALTER TABLE dbo.PayrollYearParameters ADD ParentDeductionRatePercent decimal(8,4) NOT NULL CONSTRAINT DF_PayrollYearParameters_ParentDeductionRatePercent DEFAULT 5;
    END

    IF COL_LENGTH(N'dbo.PayrollYearParameters', N'ParentAnnualDeductionCap') IS NULL
    BEGIN
        ALTER TABLE dbo.PayrollYearParameters ADD ParentAnnualDeductionCap decimal(18,3) NOT NULL CONSTRAINT DF_PayrollYearParameters_ParentAnnualDeductionCap DEFAULT 450;
    END
END

IF OBJECT_ID(N'dbo.Employees', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Employees', N'StudentChildren') IS NULL
    BEGIN
        ALTER TABLE dbo.Employees ADD StudentChildren int NOT NULL CONSTRAINT DF_Employees_StudentChildren DEFAULT 0;
    END

    IF COL_LENGTH(N'dbo.Employees', N'DisabledChildren') IS NULL
    BEGIN
        ALTER TABLE dbo.Employees ADD DisabledChildren int NOT NULL CONSTRAINT DF_Employees_DisabledChildren DEFAULT 0;
    END

    IF COL_LENGTH(N'dbo.Employees', N'DependentParents') IS NULL
    BEGIN
        ALTER TABLE dbo.Employees ADD DependentParents int NOT NULL CONSTRAINT DF_Employees_DependentParents DEFAULT 0;
    END
END

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260716030504_AddPayrollRegimeSectorFamily_Tenant')
BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20260716030504_AddPayrollRegimeSectorFamily_Tenant', N'8.0.1');
END
