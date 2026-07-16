BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715120000_AddPayrollComplianceOptions_Tenant'
)
BEGIN
    ALTER TABLE [PayrollYearParameters] ADD [CnssEmployeeRateRsa] decimal(8,4) NOT NULL DEFAULT 9.18;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715120000_AddPayrollComplianceOptions_Tenant'
)
BEGIN
    ALTER TABLE [PayrollYearParameters] ADD [CnssEmployerRateRsa] decimal(8,4) NOT NULL DEFAULT 16.57;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715120000_AddPayrollComplianceOptions_Tenant'
)
BEGIN
    ALTER TABLE [PayrollYearParameters] ADD [EnableAllowanceQuadrantMatrix] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715120000_AddPayrollComplianceOptions_Tenant'
)
BEGIN
    ALTER TABLE [PayrollYearParameters] ADD [EnableExtendedOvertimeRates] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715120000_AddPayrollComplianceOptions_Tenant'
)
BEGIN
    ALTER TABLE [PayrollYearParameters] ADD [EnforceSmigOnContracts] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715120000_AddPayrollComplianceOptions_Tenant'
)
BEGIN
    UPDATE PayrollYearParameters
    SET CnssEmployeeRateRsa = CnssEmployeeRate,
        CnssEmployerRateRsa = CnssEmployerRate
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715120000_AddPayrollComplianceOptions_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260715120000_AddPayrollComplianceOptions_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

