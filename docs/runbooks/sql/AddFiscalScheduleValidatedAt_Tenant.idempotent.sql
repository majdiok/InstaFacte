-- Idempotent tenant migration: AddFiscalScheduleValidatedAt_Tenant
-- Adds ValidatedAt / ValidatedBy to FiscalScheduleEntries when missing.

IF OBJECT_ID(N'dbo.FiscalScheduleEntries', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.FiscalScheduleEntries', N'ValidatedAt') IS NULL
    BEGIN
        ALTER TABLE dbo.FiscalScheduleEntries ADD ValidatedAt datetime2 NULL;
    END

    IF COL_LENGTH(N'dbo.FiscalScheduleEntries', N'ValidatedBy') IS NULL
    BEGIN
        ALTER TABLE dbo.FiscalScheduleEntries ADD ValidatedBy nvarchar(200) NULL;
    END
END

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260710040719_AddFiscalScheduleValidatedAt_Tenant')
BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20260710040719_AddFiscalScheduleValidatedAt_Tenant', N'8.0.1');
END
