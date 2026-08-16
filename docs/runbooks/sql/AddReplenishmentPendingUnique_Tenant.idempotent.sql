-- ============================================================================
-- AddReplenishmentPendingUnique_Tenant — script idempotent (production / DBA)
-- Équivalent SQL de la migration 20260816150000_AddReplenishmentPendingUnique_Tenant.
--
-- Objet : empêcher les doublons de recommandations « En attente » (fix C4).
--   1) Déduplication : seule la Pending la plus récente par couple
--      (ProductId, WarehouseId) est conservée ; les doublons passent à Superseded
--      (aucune suppression — l'historique est préservé).
--   2) Index unique filtré sur (ProductId, WarehouseId) WHERE Status = 1.
--
-- Statuts (stockage int) : Pending = 1, Approved = 2, Dismissed = 3,
-- Ordered = 4, Superseded = 5.
-- Réexécutable sans risque. Ne restaure pas les lignes dédupliquées.
-- ============================================================================

IF OBJECT_ID(N'[dbo].[ReplenishmentRecommendations]', N'U') IS NOT NULL
BEGIN
    -- 1) Déduplication préalable (obligatoire avant l'index unique).
    ;WITH ranked AS (
        SELECT [Id],
               ROW_NUMBER() OVER (
                   PARTITION BY [ProductId], [WarehouseId]
                   ORDER BY [GeneratedAt] DESC, [Id] DESC) AS [rn]
        FROM [dbo].[ReplenishmentRecommendations]
        WHERE [Status] = 1
    )
    UPDATE r
       SET r.[Status] = 5
      FROM [dbo].[ReplenishmentRecommendations] r
      INNER JOIN ranked d ON d.[Id] = r.[Id]
     WHERE d.[rn] > 1;

    PRINT CONCAT('Déduplication terminée : ', @@ROWCOUNT, ' doublon(s) passé(s) à Superseded.');

    -- 2) Index unique filtré.
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE [name] = N'UX_ReplenishmentRecommendations_Pending_ProductWarehouse'
          AND [object_id] = OBJECT_ID(N'[dbo].[ReplenishmentRecommendations]'))
    BEGIN
        CREATE UNIQUE INDEX [UX_ReplenishmentRecommendations_Pending_ProductWarehouse]
            ON [dbo].[ReplenishmentRecommendations] ([ProductId], [WarehouseId])
            WHERE [Status] = 1;
        PRINT 'Index UX_ReplenishmentRecommendations_Pending_ProductWarehouse créé.';
    END
    ELSE
    BEGIN
        PRINT 'Index déjà présent — rien à faire.';
    END
END
ELSE
BEGIN
    PRINT 'Table ReplenishmentRecommendations absente — module Prévisions IA non déployé.';
END
GO

-- Vérification : ne doit retourner aucune ligne.
SELECT [ProductId], [WarehouseId], COUNT(*) AS PendingCount
FROM [dbo].[ReplenishmentRecommendations]
WHERE [Status] = 1
GROUP BY [ProductId], [WarehouseId]
HAVING COUNT(*) > 1;
GO

-- Vérification : l'index doit exister et être unique + filtré.
SELECT [name], [is_unique], [has_filter], [filter_definition]
FROM sys.indexes
WHERE [name] = N'UX_ReplenishmentRecommendations_Pending_ProductWarehouse';
GO
