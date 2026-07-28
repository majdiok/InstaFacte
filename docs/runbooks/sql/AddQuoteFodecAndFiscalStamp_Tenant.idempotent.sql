-- Vague 0 — correctif 2 : FODEC et timbre fiscal sur le devis.
--
-- A executer sur CHAQUE base tenant si la migration EF ne peut pas etre appliquee par l'API.
-- Idempotent : rejouable sans effet de bord.
--
-- Strictement additif et SANS recalcul retroactif : les colonnes naissent a zero, aucun devis
-- existant n'est modifie (on ne mute jamais un document deja emis). Les devis reprendront le
-- calcul complet a leur prochain enregistrement.
--
-- Inclut un durcissement : la migration 20260713171729_AddInvoiceFodec_Tenant avait cree les
-- colonnes de devise FODEC avec defaultValue '' sans backfill. Une devise vide fait echouer
-- toute operation Money.Add cote applicatif. On la normalise a 'TND'.

BEGIN TRANSACTION;
GO

-- ─── En-tete du devis ───
IF NOT EXISTS (SELECT * FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[Quotes]') AND [name] = N'FodecAmount')
BEGIN
    ALTER TABLE [Quotes] ADD [FodecAmount] decimal(18,3) NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[Quotes]') AND [name] = N'FodecAmountCurrency')
BEGIN
    ALTER TABLE [Quotes] ADD [FodecAmountCurrency] nvarchar(3) NOT NULL DEFAULT N'TND';
END;
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[Quotes]') AND [name] = N'FiscalStampAmount')
BEGIN
    ALTER TABLE [Quotes] ADD [FiscalStampAmount] decimal(18,3) NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[Quotes]') AND [name] = N'FiscalStampAmountCurrency')
BEGIN
    ALTER TABLE [Quotes] ADD [FiscalStampAmountCurrency] nvarchar(3) NOT NULL DEFAULT N'TND';
END;
GO

-- ─── Lignes du devis ───
IF NOT EXISTS (SELECT * FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[QuoteLines]') AND [name] = N'FodecAmount')
BEGIN
    ALTER TABLE [QuoteLines] ADD [FodecAmount] decimal(18,3) NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[QuoteLines]') AND [name] = N'FodecAmountCurrency')
BEGIN
    ALTER TABLE [QuoteLines] ADD [FodecAmountCurrency] nvarchar(3) NOT NULL DEFAULT N'TND';
END;
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[QuoteLines]') AND [name] = N'IsFodecApplicable')
BEGIN
    ALTER TABLE [QuoteLines] ADD [IsFodecApplicable] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[QuoteLines]') AND [name] = N'FodecRatePercent')
BEGIN
    ALTER TABLE [QuoteLines] ADD [FodecRatePercent] decimal(5,2) NOT NULL DEFAULT 1.0;
END;
GO

-- ─── Durcissement : devises FODEC vides heritees de 20260713171729 ───
UPDATE [Invoices]     SET [FodecAmountCurrency] = N'TND' WHERE ISNULL([FodecAmountCurrency], '') = '';
UPDATE [InvoiceLines] SET [FodecAmountCurrency] = N'TND' WHERE ISNULL([FodecAmountCurrency], '') = '';
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727120000_AddQuoteFodecAndFiscalStamp_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260727120000_AddQuoteFodecAndFiscalStamp_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

-- Verification
-- SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Quotes'
--   AND COLUMN_NAME IN ('FodecAmount','FodecAmountCurrency','FiscalStampAmount','FiscalStampAmountCurrency');
-- SELECT COUNT(*) AS DevisesFodecVides FROM [Invoices] WHERE ISNULL([FodecAmountCurrency],'') = ''; -- doit valoir 0
