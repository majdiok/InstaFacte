-- Master DB : moteur d'inférence Ollama (GPU auto / CPU uniquement) sur PlatformAiSettings.
-- Idempotent : peut être rejoué sans erreur si la colonne ou l'entrée d'historique existe déjà.

BEGIN TRANSACTION;
GO

IF COL_LENGTH('PlatformAiSettings', 'InferenceDevice') IS NULL
BEGIN
    ALTER TABLE [PlatformAiSettings]
        ADD [InferenceDevice] int NOT NULL CONSTRAINT [DF_PlatformAiSettings_InferenceDevice] DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610235647_AddInferenceDevice_Master'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260610235647_AddInferenceDevice_Master', N'8.0.1');
END;
GO

COMMIT;
GO