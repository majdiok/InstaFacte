-- Master DB : modèle dédié à l'Assistant Studio sur PlatformAiSettings.
-- Jumeau (rattrapage) de la migration EF 20260729020000_AddStudioAiModelRef_Master, livrée sans
-- script idempotent : ce fichier permet de rattraper une base master où la migration n'a pas pu
-- être appliquée par EF (déploiement manuel, restauration partielle).
-- Idempotent : peut être rejoué sans erreur si la colonne ou l'entrée d'historique existe déjà.
--
-- Colonne NULLABLE et purement additive : NULL = « aucun modèle Studio dédié », la résolution
-- retombe sur Ollama:StudioAiModel puis le modèle Assistant plateforme puis Ollama:DefaultModel.

BEGIN TRANSACTION;
GO

IF COL_LENGTH('PlatformAiSettings', 'StudioAiModelRef') IS NULL
BEGIN
    ALTER TABLE [PlatformAiSettings]
        ADD [StudioAiModelRef] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729020000_AddStudioAiModelRef_Master'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260729020000_AddStudioAiModelRef_Master', N'8.0.1');
END;
GO

COMMIT;
GO
