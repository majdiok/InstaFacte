-- Idempotent tenant migration: AddStudioAiBuildPlans_Tenant
-- MigrationId: 20260728001141_AddStudioAiBuildPlans_Tenant
-- Nouvelle table isolée (plans Studio IA « aperçu → confirmation ») : aucune dépendance
-- sur les tables Studio existantes, aucune colonne ajoutée à une table existante.

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728001141_AddStudioAiBuildPlans_Tenant'
)
BEGIN
    RETURN;
END;
GO

IF OBJECT_ID(N'StudioAiBuildPlans', N'U') IS NULL
BEGIN
    CREATE TABLE [StudioAiBuildPlans] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Kind] int NOT NULL,
        [SpecJson] nvarchar(max) NOT NULL,
        [SummaryJson] nvarchar(max) NOT NULL,
        [Status] int NOT NULL,
        [ErrorMessage] nvarchar(2048) NULL,
        [ResultJson] nvarchar(max) NULL,
        [CreatedBy] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [ExecutedAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_StudioAiBuildPlans] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE name = N'IX_StudioAiBuildPlans_TenantId_Status_CreatedAt'
      AND object_id = OBJECT_ID(N'StudioAiBuildPlans')
)
BEGIN
    CREATE INDEX [IX_StudioAiBuildPlans_TenantId_Status_CreatedAt]
        ON [StudioAiBuildPlans] ([TenantId], [Status], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728001141_AddStudioAiBuildPlans_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260728001141_AddStudioAiBuildPlans_Tenant', N'8.0.1');
END;
GO
