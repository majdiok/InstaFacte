-- Migration : grilles tarifaires (PriceLists) -> prix negocies par client (ClientProductPrices)
--
-- A executer sur CHAQUE base tenant AVANT de masquer le module Grilles tarifaires dans l'UI.
-- Idempotent : les couples client/produit deja presents ne sont pas dupliques.
--
-- Etapes :
--   1. Pour chaque client affecte a une grille, copier chaque prix produit de la grille
--      vers ClientProductPrices (si absent).
--   2. Desactiver toutes les grilles migrees.
--   3. Retirer l'affectation PriceListId des clients.

BEGIN TRANSACTION;
GO

-- 1. Copie des prix grille -> prix negocie client/produit
INSERT INTO [ClientProductPrices] (
    [Id],
    [ClientId],
    [ProductId],
    [UnitPriceHT],
    [UnitPriceHTCurrency],
    [IsActive],
    [ValidFrom],
    [ValidUntil],
    [CreatedAt],
    [UpdatedAt]
)
SELECT
    NEWID(),
    c.[Id],
    pli.[ProductId],
    pli.[UnitPriceHT],
    pli.[UnitPriceHTCurrency],
    CASE WHEN pl.[IsActive] = 1 THEN 1 ELSE 0 END,
    pl.[ValidFrom],
    pl.[ValidUntil],
    GETUTCDATE(),
    NULL
FROM [Clients] c
INNER JOIN [PriceLists] pl ON pl.[Id] = c.[PriceListId]
INNER JOIN [PriceListItems] pli ON pli.[PriceListId] = pl.[Id]
WHERE c.[PriceListId] IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM [ClientProductPrices] cpp
      WHERE cpp.[ClientId] = c.[Id]
        AND cpp.[ProductId] = pli.[ProductId]
  );
GO

-- 2. Desactivation des grilles encore affectees
UPDATE pl
SET pl.[IsActive] = 0,
    pl.[UpdatedAt] = GETUTCDATE()
FROM [PriceLists] pl
WHERE EXISTS (
    SELECT 1 FROM [Clients] c WHERE c.[PriceListId] = pl.[Id]
);
GO

-- 3. Retrait de l'affectation grille sur les clients
UPDATE [Clients]
SET [PriceListId] = NULL,
    [UpdatedAt] = GETUTCDATE()
WHERE [PriceListId] IS NOT NULL;
GO

COMMIT TRANSACTION;
GO
