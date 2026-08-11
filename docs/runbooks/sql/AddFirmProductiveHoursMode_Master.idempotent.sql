-- Idempotent : mode de calcul des heures productives et dates de présence (base MASTER).
-- Correspond à la migration 20260811030000_AddFirmProductiveHoursMode_Master.
--
-- Non-régression stricte : le mode est créé à 0 (Parametric) sur tous les exercices existants et
-- les dates de présence restent nulles, ce qui vaut « présent toute l'année ». Aucun taux horaire,
-- donc aucune marge déjà calculée, ne change tant qu'un exercice n'est pas basculé explicitement.

IF COL_LENGTH('FirmTimeSheetYearSettings', 'ProductiveHoursMode') IS NULL
BEGIN
    ALTER TABLE FirmTimeSheetYearSettings ADD ProductiveHoursMode int NOT NULL CONSTRAINT DF_FirmTimeSheetYearSettings_ProductiveHoursMode DEFAULT 0;
END
GO

IF COL_LENGTH('FirmCollaboratorProfiles', 'HiredOn') IS NULL
BEGIN
    ALTER TABLE FirmCollaboratorProfiles ADD HiredOn datetime2 NULL;
END
GO

IF COL_LENGTH('FirmCollaboratorProfiles', 'LeftOn') IS NULL
BEGIN
    ALTER TABLE FirmCollaboratorProfiles ADD LeftOn datetime2 NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE MigrationId = '20260811030000_AddFirmProductiveHoursMode_Master')
BEGIN
    INSERT INTO [__EFMigrationsHistory] (MigrationId, ProductVersion)
    VALUES ('20260811030000_AddFirmProductiveHoursMode_Master', '8.0.11');
END
GO
