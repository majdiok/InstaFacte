-- Idempotent tenant migration: AddPayrollRunTotalNetTaxable_Tenant
-- MigrationId: 20260827120000_AddPayrollRunTotalNetTaxable_Tenant
--
-- Total du net imposable salarial figé sur le cycle (assiette articles 1 et 3 du formulaire
-- officiel : IRPP et CSS). Reprise depuis Payslips.MonthlyNetTaxable pour les cycles déjà arrêtés.
--
-- À exécuter sur CHAQUE base tenant si la migration EF ne peut pas être appliquée par l'API.
-- Rejouable : chaque bloc est gardé, l'exécuter deux fois est sans effet.

IF OBJECT_ID(N'dbo.PayrollRuns', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.PayrollRuns', N'TotalNetTaxable') IS NULL
BEGIN
    ALTER TABLE dbo.PayrollRuns
        ADD TotalNetTaxable decimal(18,3) NOT NULL
        CONSTRAINT DF_PayrollRuns_TotalNetTaxable DEFAULT (0);
END

IF OBJECT_ID(N'dbo.PayrollRuns', N'U') IS NOT NULL
AND OBJECT_ID(N'dbo.Payslips', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.PayrollRuns', N'TotalNetTaxable') IS NOT NULL
BEGIN
    UPDATE r
    SET r.TotalNetTaxable = ISNULL((
        SELECT SUM(p.MonthlyNetTaxable)
        FROM dbo.Payslips p
        WHERE p.PayrollRunId = r.Id
    ), 0)
    FROM dbo.PayrollRuns r;
END

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260827120000_AddPayrollRunTotalNetTaxable_Tenant')
BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20260827120000_AddPayrollRunTotalNetTaxable_Tenant', N'8.0.1');
END
