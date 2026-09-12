-- Idempotent tenant migration: AddStudioRecordViews_Tenant
-- MigrationId: 20260912140000_AddStudioRecordViews_Tenant
-- Studio IA — PR 2.3 (vues enregistrées) : crée la table CustomRecordViewDefinitions
-- (vues Liste / Kanban / Calendrier d'une table Studio : clé, libellé, Mode int défaut 0 = List,
-- DefinitionJson, IsDefault, suppression logique), sa FK en cascade vers CustomEntityDefinitions,
-- l'index unique filtré (TenantId, EntityDefinitionId, Key) WHERE IsDeleted = 0 et l'index
-- (TenantId, EntityDefinitionId, IsDefault). Aucune ligne existante n'est modifiée.

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260912140000_AddStudioRecordViews_Tenant'
)
BEGIN
    RETURN;
END;
GO

IF OBJECT_ID(N'CustomEntityDefinitions', N'U') IS NULL
BEGIN
    RAISERROR(N'CustomEntityDefinitions table is missing: apply prior Studio migrations first.', 16, 1);
    RETURN;
END;
GO

IF OBJECT_ID(N'[dbo].[CustomRecordViewDefinitions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CustomRecordViewDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [EntityDefinitionId] uniqueidentifier NOT NULL,
        [Key] nvarchar(64) NOT NULL,
        [DisplayName] nvarchar(128) NOT NULL,
        [Mode] int NOT NULL CONSTRAINT [DF_CustomRecordViewDefinitions_Mode] DEFAULT (0),
        [DefinitionJson] nvarchar(max) NOT NULL,
        [IsDefault] bit NOT NULL CONSTRAINT [DF_CustomRecordViewDefinitions_IsDefault] DEFAULT (0),
        [IsActive] bit NOT NULL CONSTRAINT [DF_CustomRecordViewDefinitions_IsActive] DEFAULT (1),
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_CustomRecordViewDefinitions_IsDeleted] DEFAULT (0),
        [DeletedAt] datetime2 NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CustomRecordViewDefinitions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomRecordViewDefinitions_CustomEntityDefinitions] FOREIGN KEY ([EntityDefinitionId])
            REFERENCES [dbo].[CustomEntityDefinitions] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE name = N'UX_CustomRecordViewDefinitions_Tenant_Entity_Key'
      AND object_id = OBJECT_ID(N'[dbo].[CustomRecordViewDefinitions]')
)
BEGIN
    CREATE UNIQUE INDEX [UX_CustomRecordViewDefinitions_Tenant_Entity_Key]
        ON [dbo].[CustomRecordViewDefinitions] ([TenantId], [EntityDefinitionId], [Key])
        WHERE [IsDeleted] = 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE name = N'IX_CustomRecordViewDefinitions_Tenant_Entity_Default'
      AND object_id = OBJECT_ID(N'[dbo].[CustomRecordViewDefinitions]')
)
BEGIN
    CREATE INDEX [IX_CustomRecordViewDefinitions_Tenant_Entity_Default]
        ON [dbo].[CustomRecordViewDefinitions] ([TenantId], [EntityDefinitionId], [IsDefault]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260912140000_AddStudioRecordViews_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260912140000_AddStudioRecordViews_Tenant', N'8.0.1');
END;
GO
