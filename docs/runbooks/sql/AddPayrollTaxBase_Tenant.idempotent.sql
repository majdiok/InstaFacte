-- Idempotent tenant migration: AddPayrollTaxBase_Tenant
-- MigrationId: 20260810120000_AddPayrollTaxBase_Tenant
--
-- Assiette et taux des taxes sur salaires (TFP, FOPROLOS, CSS patronale), figés au calcul.
-- Colonnes nullables sur Payslips et PayrollRuns : les bulletins et cycles antérieurs
-- restent intacts, la déclaration retombant sur une reconstitution pour ceux-là.
-- ApplyCnssCeilingToPayrollTaxes vaut true par défaut (comportement historique).
--
-- À exécuter sur CHAQUE base tenant si la migration EF ne peut pas être appliquée par l'API.
-- Rejouable : chaque bloc est gardé, l'exécuter deux fois est sans effet.

IF OBJECT_ID(N'dbo.Payslips', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Payslips', N'PayrollTaxBase') IS NULL
        ALTER TABLE dbo.Payslips ADD PayrollTaxBase decimal(18,3) NULL;

    IF COL_LENGTH(N'dbo.Payslips', N'AppliedTfpRate') IS NULL
        ALTER TABLE dbo.Payslips ADD AppliedTfpRate decimal(8,4) NULL;
END

IF OBJECT_ID(N'dbo.PayrollRuns', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.PayrollRuns', N'TotalPayrollTaxBase') IS NULL
        ALTER TABLE dbo.PayrollRuns ADD TotalPayrollTaxBase decimal(18,3) NULL;

    IF COL_LENGTH(N'dbo.PayrollRuns', N'AppliedTfpRate') IS NULL
        ALTER TABLE dbo.PayrollRuns ADD AppliedTfpRate decimal(8,4) NULL;
END

IF OBJECT_ID(N'dbo.PayrollYearParameters', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.PayrollYearParameters', N'ApplyCnssCeilingToPayrollTaxes') IS NULL
        ALTER TABLE dbo.PayrollYearParameters
            ADD ApplyCnssCeilingToPayrollTaxes bit NOT NULL
            CONSTRAINT DF_PayrollYearParameters_ApplyCnssCeilingToPayrollTaxes DEFAULT 1;
END

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260810120000_AddPayrollTaxBase_Tenant')
BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20260810120000_AddPayrollTaxBase_Tenant', N'8.0.1');
END

-- Vérification :
-- SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId LIKE '%AddPayrollTaxBase%';
-- SELECT COL_LENGTH('dbo.PayrollYearParameters', 'ApplyCnssCeilingToPayrollTaxes') AS ColumnExists;
