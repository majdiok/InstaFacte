-- Tenant DB : statut brouillard/validation des écritures comptables (JournalEntries).
-- Colonnes : Status (0=Brouillon, 1=Validee, 2=Cloturee), ValidatedAt, ValidatedBy + index sur Status.
-- Rétro-compatibilité : le DEFAULT 1 (Validee) backfille les écritures existantes → états inchangés.
-- Idempotent : peut être rejoué sans erreur si les colonnes / l'index / l'entrée d'historique existent déjà.

BEGIN TRANSACTION;
GO

IF COL_LENGTH('JournalEntries', 'Status') IS NULL
BEGIN
    ALTER TABLE [JournalEntries]
        ADD [Status] int NOT NULL CONSTRAINT [DF_JournalEntries_Status] DEFAULT 1;
END;
GO

IF COL_LENGTH('JournalEntries', 'ValidatedAt') IS NULL
BEGIN
    ALTER TABLE [JournalEntries] ADD [ValidatedAt] datetime2 NULL;
END;
GO

IF COL_LENGTH('JournalEntries', 'ValidatedBy') IS NULL
BEGIN
    ALTER TABLE [JournalEntries] ADD [ValidatedBy] nvarchar(256) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_JournalEntries_Status' AND object_id = OBJECT_ID(N'[JournalEntries]')
)
BEGIN
    CREATE INDEX [IX_JournalEntries_Status] ON [JournalEntries] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260705180716_AddJournalEntryStatus_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260705180716_AddJournalEntryStatus_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO
