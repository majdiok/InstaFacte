-- Vague 1, lot 7 — régime de TVA du client et attestation de suspension.
--
-- A executer sur CHAQUE base tenant si la migration EF ne peut pas etre appliquee par l'API.
-- Idempotent : rejouable sans effet de bord.
--
-- Contexte : exoneration, suspension et export etaient tous amalgames dans « 0 % » : impossible
-- de distinguer un client exportateur d'un client exonere, ni de justifier une suspension
-- (art. 11 du code de la TVA) en controle. Le regime devient un attribut du CLIENT ; le taux de
-- ligne (VatRate) n'est pas touche.
--
-- Strictement additif : VatRegime NOT NULL default 0 (Normal) — les clients existants gardent
-- exactement leur comportement de facturation — trois colonnes nullables pour l'attestation, et
-- un index filtre ecartant les assujettis ordinaires.

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE [object_id] = OBJECT_ID(N'[Clients]') AND [name] = N'VatRegime'
)
BEGIN
    ALTER TABLE [Clients] ADD [VatRegime] int NOT NULL CONSTRAINT [DF_Clients_VatRegime] DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE [object_id] = OBJECT_ID(N'[Clients]') AND [name] = N'VatExemptionCertificateNumber'
)
BEGIN
    ALTER TABLE [Clients] ADD [VatExemptionCertificateNumber] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE [object_id] = OBJECT_ID(N'[Clients]') AND [name] = N'VatExemptionValidFrom'
)
BEGIN
    ALTER TABLE [Clients] ADD [VatExemptionValidFrom] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE [object_id] = OBJECT_ID(N'[Clients]') AND [name] = N'VatExemptionValidUntil'
)
BEGIN
    ALTER TABLE [Clients] ADD [VatExemptionValidUntil] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE [name] = N'IX_Clients_VatRegime' AND [object_id] = OBJECT_ID(N'[Clients]')
)
BEGIN
    CREATE INDEX [IX_Clients_VatRegime]
        ON [Clients] ([VatRegime])
        WHERE [VatRegime] <> 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729140000_AddClientVatRegime_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260729140000_AddClientVatRegime_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO
