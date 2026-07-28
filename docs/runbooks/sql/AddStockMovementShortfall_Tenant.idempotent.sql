-- Vague 0 — correctif 7 : tracabilite des ruptures de stock (StockMovements.ShortfallQuantity).
--
-- A executer sur CHAQUE base tenant si la migration EF ne peut pas etre appliquee par l'API.
-- Idempotent : rejouable sans effet de bord.
--
-- Le COMPORTEMENT FONCTIONNEL EST INCHANGE : une vente en rupture n'est toujours pas bloquee,
-- la sortie reste limitee au stock disponible. La colonne rend seulement l'ecart mesurable.
--
-- Nullable sans valeur par defaut : les mouvements existants restent NULL, ce qui signifie
-- « aucune rupture constatee » — exact, l'information n'etait pas collectee auparavant.

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE [object_id] = OBJECT_ID(N'[StockMovements]') AND [name] = N'ShortfallQuantity'
)
BEGIN
    ALTER TABLE [StockMovements] ADD [ShortfallQuantity] decimal(18,4) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727140000_AddStockMovementShortfall_Tenant'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260727140000_AddStockMovementShortfall_Tenant', N'8.0.1');
END;
GO

COMMIT;
GO

-- Suivi operationnel : ruptures constatees depuis la mise en service.
-- SELECT m.OccurredAt, m.Reference, m.Quantity AS Sorti, m.ShortfallQuantity AS NonHonore,
--        p.Code, p.Name, w.Name AS Entrepot
-- FROM [StockMovements] m
-- INNER JOIN [StockItems] si ON si.Id = m.StockItemId
-- INNER JOIN [Products]   p  ON p.Id  = si.ProductId
-- INNER JOIN [Warehouses] w  ON w.Id  = si.WarehouseId
-- WHERE m.ShortfallQuantity > 0
-- ORDER BY m.OccurredAt DESC;
