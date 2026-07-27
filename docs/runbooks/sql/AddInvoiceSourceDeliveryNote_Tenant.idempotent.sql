-- Vague 0 — correctif 1 : lien typé bon de livraison -> facture (Invoices.SourceDeliveryNoteId).
--
-- A executer sur CHAQUE base tenant si la migration EF ne peut pas etre appliquee par l'API.
-- Idempotent : rejouable sans effet de bord. Ne marque PAS la migration comme appliquee dans
-- __EFMigrationsHistory tant que le schema n'est pas conforme, afin que l'API puisse encore
-- la rejouer si besoin.
--
-- Contexte : le garde-fou anti-double-deduction de stock reposait sur un prefixe de chaine
-- dans Invoices.Reference (champ libre). Le backfill s'appuie sur DeliveryNotes.InvoiceId,
-- relation inverse deja persistee, donc exhaustif et sans heuristique de texte.

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE [object_id] = OBJECT_ID(N'[Invoices]') AND [name] = N'SourceDeliveryNoteId'
)
BEGIN
    ALTER TABLE [Invoices] ADD [SourceDeliveryNoteId] uniqueidentifier NULL;
END;
GO

-- Backfill : rattache chaque facture au bon de livraison qui la reference.
UPDATE i
SET i.SourceDeliveryNoteId = dn.Id
FROM [Invoices] i
INNER JOIN [DeliveryNotes] dn ON dn.InvoiceId = i.Id
WHERE i.SourceDeliveryNoteId IS NULL;
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE [name] = N'IX_Invoices_SourceDeliveryNoteId' AND [object_id] = OBJECT_ID(N'[Invoices]')
)
BEGIN
    CREATE INDEX [IX_Invoices_SourceDeliveryNoteId]
        ON [Invoices] ([SourceDeliveryNoteId])
        WHERE [SourceDeliveryNoteId] IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727100000_AddInvoiceSourceDeliveryNote_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260727100000_AddInvoiceSourceDeliveryNote_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

-- Verification
-- SELECT COUNT(*) AS FacturesIssuesDeBL FROM [Invoices] WHERE [SourceDeliveryNoteId] IS NOT NULL;
-- SELECT COUNT(*) AS BLFactures        FROM [DeliveryNotes] WHERE [InvoiceId] IS NOT NULL;
-- Les deux comptes doivent etre egaux.
