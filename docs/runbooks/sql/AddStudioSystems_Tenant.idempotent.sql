-- Idempotent tenant migration: AddStudioSystems_Tenant
-- MigrationId: 20260624181553_AddStudioSystems_Tenant
-- Requires CustomEntityDefinitions (AddStudioLowCode_Tenant) to exist first.

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260624181553_AddStudioSystems_Tenant'
)
BEGIN
    RETURN;
END;
GO

IF OBJECT_ID(N'CustomEntityDefinitions', N'U') IS NULL
BEGIN
    RAISERROR(N'CustomEntityDefinitions table missing — apply prior Studio migrations first.', 16, 1);
    RETURN;
END;
GO

IF COL_LENGTH('CustomEntityDefinitions', 'SystemId') IS NULL
BEGIN
    ALTER TABLE [CustomEntityDefinitions] ADD [SystemId] uniqueidentifier NULL;
END;
GO

IF OBJECT_ID(N'CustomSystemDefinitions', N'U') IS NULL
BEGIN
    CREATE TABLE [CustomSystemDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Key] nvarchar(64) NOT NULL,
        [DisplayName] nvarchar(128) NOT NULL,
        [Icon] nvarchar(64) NULL,
        [Description] nvarchar(1024) NULL,
        [OnboardingJson] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CustomSystemDefinitions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE name = N'IX_CustomEntityDefinitions_TenantId_SystemId'
      AND object_id = OBJECT_ID(N'CustomEntityDefinitions')
)
BEGIN
    CREATE INDEX [IX_CustomEntityDefinitions_TenantId_SystemId]
        ON [CustomEntityDefinitions] ([TenantId], [SystemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE name = N'IX_CustomSystemDefinitions_TenantId_Key'
      AND object_id = OBJECT_ID(N'CustomSystemDefinitions')
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomSystemDefinitions_TenantId_Key]
        ON [CustomSystemDefinitions] ([TenantId], [Key]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260624181553_AddStudioSystems_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260624181553_AddStudioSystems_Tenant', N'8.0.1');
END;
GO
