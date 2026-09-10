-- Master DB : modèle Studio « avancé » (GPU distant / cloud) sur PlatformAiSettings.
-- Jumeau de la migration EF 20260910120000_AddStudioAiAdvancedModelRef_Master (Studio IA, PR 1.1).
-- Idempotent : peut être rejoué sans erreur si la colonne ou l'entrée d'historique existe déjà.
--
-- Colonne NULLABLE et purement additive : NULL = aucun modèle avancé configuré, la capacité
-- StudioAiCapabilitiesDto.AdvancedModelAvailable reste false et la résolution du modèle Studio
-- (StudioAiModelRef → Ollama:StudioAiModel → DefaultModelRef → Ollama:DefaultModel) est inchangée.
--
-- Cutover ops (après cette migration) :
-- 1. Back-office plateforme > Configuration IA > « Modèle IA — Assistant Studio » : renseigner
--    « Modèle Studio avancé (GPU / cloud) », qui doit DIFFÉRER du modèle Studio standard.
-- 2. Activer Ollama:EnableStudioAiAdvancedModel (appsettings) pour exposer la bascule au Studio.

BEGIN TRANSACTION;
GO

IF COL_LENGTH('PlatformAiSettings', 'StudioAiAdvancedModelRef') IS NULL
BEGIN
    ALTER TABLE [PlatformAiSettings]
        ADD [StudioAiAdvancedModelRef] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910120000_AddStudioAiAdvancedModelRef_Master'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260910120000_AddStudioAiAdvancedModelRef_Master', N'8.0.1');
END;
GO

COMMIT;
GO
