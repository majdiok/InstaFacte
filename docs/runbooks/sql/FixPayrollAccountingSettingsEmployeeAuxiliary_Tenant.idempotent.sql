-- Idempotent tenant migration: FixPayrollAccountingSettingsEmployeeAuxiliary_Tenant
-- MigrationId: 20260902170000_FixPayrollAccountingSettingsEmployeeAuxiliary_Tenant
--
-- Rattrape PayrollAccountingSettings.EmployeeAuxiliaryEnabled sur les bases où la table a été créée
-- sans cette colonne : 20260901160000_AddPayrollAccountingSettings_Tenant y a été appliquée dans un
-- état antérieur à son ajout, puis complétée sur place. EF ne rejoue pas une migration déjà
-- inscrite, si bien que toute lecture du réglage échoue en « Invalid column name
-- 'EmployeeAuxiliaryEnabled' » (SQL 207) : validation de cycle de paie, écran Paramètres paie,
-- journal de paie.
--
-- Le défaut 1 reprend celui de la migration d'origine, du domaine et de la configuration globale
-- (Accounting:PayrollEmployeeAuxiliaryEnabled) : aucune imputation comptable ne change. La table est
-- un singleton par tenant, vide tant que le dossier n'est pas paramétré — aucune reprise de données.
--
-- À exécuter sur CHAQUE base tenant si la migration EF ne peut pas être appliquée par l'API.
-- Rejouable : chaque bloc est gardé, l'exécuter deux fois est sans effet.

IF OBJECT_ID(N'dbo.PayrollAccountingSettings', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.PayrollAccountingSettings', N'EmployeeAuxiliaryEnabled') IS NULL
        ALTER TABLE dbo.PayrollAccountingSettings
            ADD EmployeeAuxiliaryEnabled bit NOT NULL
            CONSTRAINT DF_PayrollAccountingSettings_EmployeeAuxiliaryEnabled DEFAULT CONVERT(bit, 1);
END

-- L'inscription dans l'historique est conditionnée à la présence effective de la colonne. Sur une
-- base où la table n'existe pas encore (migration 20260901160000 en attente), on laisse donc ce
-- rattrapage en attente lui aussi : EF appliquera les deux dans l'ordre, et la table sera créée
-- d'emblée avec la colonne.
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.PayrollAccountingSettings', N'EmployeeAuxiliaryEnabled') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N'20260902170000_FixPayrollAccountingSettingsEmployeeAuxiliary_Tenant')
BEGIN
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20260902170000_FixPayrollAccountingSettingsEmployeeAuxiliary_Tenant', N'8.0.1');
END

-- Vérification :
-- SELECT MigrationId FROM __EFMigrationsHistory
-- WHERE MigrationId LIKE '%FixPayrollAccountingSettingsEmployeeAuxiliary%';
--
-- SELECT COL_LENGTH('dbo.PayrollAccountingSettings', 'EmployeeAuxiliaryEnabled') AS ColumnExists;
--
-- -- Doit renvoyer 1 (défaut aligné sur la configuration globale) :
-- SELECT dc.definition AS DefaultDefinition
-- FROM sys.default_constraints dc
-- INNER JOIN sys.columns c
--     ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
-- WHERE dc.parent_object_id = OBJECT_ID('dbo.PayrollAccountingSettings')
--   AND c.name = 'EmployeeAuxiliaryEnabled';
