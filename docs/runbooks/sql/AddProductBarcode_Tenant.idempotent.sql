-- Vague 1, lot 8 — code-barres article (Products.Barcode).
--
-- A executer sur CHAQUE base tenant si la migration EF ne peut pas etre appliquee par l'API.
-- Idempotent : rejouable sans effet de bord.
--
-- Contexte : le produit ne portait aucun code-barres. Le scan du point de vente cherchait
-- dans le CODE PRODUIT interne, avec repli sur une correspondance approchee bidirectionnelle
-- (includes dans les deux sens) : scanner « 1234 » pouvait encaisser l'article « 12345 ».
--
-- ATTENTION : l'index est volontairement NON UNIQUE.
-- L'unicite par tenant ne doit etre livree qu'APRES un balayage des doublons sur tout le
-- parc — une migration unique en echec bloquerait le tenant au demarrage
-- (TenantMigrationGuard). Meme prudence que pour l'unicite des numeros de facture :
-- voir docs/runbooks/invoice-number-uniqueness.md.

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE [object_id] = OBJECT_ID(N'[Products]') AND [name] = N'Barcode'
)
BEGIN
    ALTER TABLE [Products] ADD [Barcode] nvarchar(13) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE [name] = N'IX_Products_Barcode' AND [object_id] = OBJECT_ID(N'[Products]')
)
BEGIN
    CREATE INDEX [IX_Products_Barcode]
        ON [Products] ([Barcode])
        WHERE [Barcode] IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260729100000_AddProductBarcode_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260729100000_AddProductBarcode_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

-- Balayage prealable a une eventuelle contrainte d'unicite (a executer sur chaque tenant) :
-- SELECT [Barcode], COUNT(*) AS n
-- FROM [Products]
-- WHERE [Barcode] IS NOT NULL
-- GROUP BY [Barcode]
-- HAVING COUNT(*) > 1;
-- Un resultat vide sur TOUS les tenants est la condition prealable a l'index UNIQUE.
