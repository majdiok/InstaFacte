-- Master DB : credentials OpenRouter partagés sur PlatformAiSettings.
-- Idempotent : peut être rejoué sans erreur si les colonnes ou l'entrée d'historique existent déjà.
--
-- Cutover ops (après cette migration) :
-- 1. Back-office plateforme > Configuration IA > section OpenRouter : saisir la clé, activer, Enregistrer.
-- 2. Vérifier modèle assistant / import (openrouter:… ou ollama:…).
-- 3. Smoke : GET /api/ai/health, chat assistant, import facture si cloud.
-- 4. Confirmer absence de la carte « Fournisseurs IA » dans Paramètres tenant.
-- 5. GET/PUT /api/ai/providers/openrouter doit répondre 404.

BEGIN TRANSACTION;
GO

IF COL_LENGTH('PlatformAiSettings', 'OpenRouterIsEnabled') IS NULL
BEGIN
    ALTER TABLE [PlatformAiSettings]
        ADD [OpenRouterIsEnabled] bit NOT NULL
            CONSTRAINT [DF_PlatformAiSettings_OpenRouterIsEnabled] DEFAULT 0;
END;
GO

IF COL_LENGTH('PlatformAiSettings', 'OpenRouterDisplayName') IS NULL
BEGIN
    ALTER TABLE [PlatformAiSettings]
        ADD [OpenRouterDisplayName] nvarchar(200) NULL;
END;
GO

IF COL_LENGTH('PlatformAiSettings', 'OpenRouterBaseUrl') IS NULL
BEGIN
    ALTER TABLE [PlatformAiSettings]
        ADD [OpenRouterBaseUrl] nvarchar(500) NULL;
END;
GO

IF COL_LENGTH('PlatformAiSettings', 'OpenRouterEncryptedApiKey') IS NULL
BEGIN
    ALTER TABLE [PlatformAiSettings]
        ADD [OpenRouterEncryptedApiKey] nvarchar(4000) NULL;
END;
GO

IF COL_LENGTH('PlatformAiSettings', 'OpenRouterApiKeyLast4') IS NULL
BEGIN
    ALTER TABLE [PlatformAiSettings]
        ADD [OpenRouterApiKeyLast4] nvarchar(4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260731100000_AddOpenRouterCredentials_Master'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260731100000_AddOpenRouterCredentials_Master', N'8.0.1');
END;
GO

COMMIT;
GO
