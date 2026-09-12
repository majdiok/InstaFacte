-- Idempotent tenant migration: AddStudioEntityKind_Tenant
-- MigrationId: 20260912130000_AddStudioEntityKind_Tenant
-- Studio IA — PR 2.1 (relations plusieurs-à-plusieurs) : ajoute la colonne
-- CustomEntityDefinitions.Kind (int NOT NULL, défaut 0 = Standard ; 1 = Junction) et l'index
-- (TenantId, Kind). Aucune ligne existante n'est modifiée (défaut SQL), aucune table créée.

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260912130000_AddStudioEntityKind_Tenant'
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

IF COL_LENGTH('CustomEntityDefinitions', 'Kind') IS NULL
BEGIN
    ALTER TABLE [CustomEntityDefinitions]
        ADD [Kind] int NOT NULL CONSTRAINT [DF_CustomEntityDefinitions_Kind] DEFAULT (0);
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE name = N'IX_CustomEntityDefinitions_TenantId_Kind'
      AND object_id = OBJECT_ID(N'CustomEntityDefinitions')
)
BEGIN
    CREATE INDEX [IX_CustomEntityDefinitions_TenantId_Kind]
        ON [CustomEntityDefinitions] ([TenantId], [Kind]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260912130000_AddStudioEntityKind_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260912130000_AddStudioEntityKind_Tenant', N'8.0.1');
END;
GO
