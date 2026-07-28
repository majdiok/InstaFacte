-- Vague 0 — correctif 4 : lien avoir -> facture d'origine (Invoices.LinkedInvoiceId).
--
-- A executer sur CHAQUE base tenant si la migration EF ne peut pas etre appliquee par l'API.
-- Idempotent : rejouable sans effet de bord.
--
-- La colonne reste NULLABLE : les avoirs historiques ne sont pas invalides. L'obligation est
-- portee par la fabrique Invoice.CreateCreditNote, donc appliquee aux NOUVEAUX avoirs seulement.
--
-- Le backfill est best-effort : il extrait le lien du JSON des brouillons convertis. Un echec
-- d'extraction est sans gravite (information de tracabilite, non structurante).

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE [object_id] = OBJECT_ID(N'[Invoices]') AND [name] = N'LinkedInvoiceId'
)
BEGIN
    ALTER TABLE [Invoices] ADD [LinkedInvoiceId] uniqueidentifier NULL;
END;
GO

-- Backfill best-effort depuis InvoiceDrafts.MetadataJson (Type = 1 => CreditNote).
UPDATE i
SET i.LinkedInvoiceId = TRY_CONVERT(uniqueidentifier, JSON_VALUE(d.MetadataJson, '$.LinkedInvoiceId'))
FROM [Invoices] i
INNER JOIN [InvoiceDrafts] d ON d.ConvertedInvoiceId = i.Id
WHERE i.LinkedInvoiceId IS NULL
  AND i.Type = 1
  AND d.MetadataJson IS NOT NULL
  AND ISJSON(d.MetadataJson) = 1
  AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(d.MetadataJson, '$.LinkedInvoiceId')) IS NOT NULL;
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE [name] = N'IX_Invoices_LinkedInvoiceId' AND [object_id] = OBJECT_ID(N'[Invoices]')
)
BEGIN
    CREATE INDEX [IX_Invoices_LinkedInvoiceId]
        ON [Invoices] ([LinkedInvoiceId])
        WHERE [LinkedInvoiceId] IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727130000_AddInvoiceLinkedInvoice_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260727130000_AddInvoiceLinkedInvoice_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

-- Verification
-- SELECT COUNT(*) AS AvoirsTotal      FROM [Invoices] WHERE [Type] = 1;
-- SELECT COUNT(*) AS AvoirsAvecLien   FROM [Invoices] WHERE [Type] = 1 AND [LinkedInvoiceId] IS NOT NULL;
-- L'ecart correspond aux avoirs historiques dont le brouillon ne portait pas le lien.
