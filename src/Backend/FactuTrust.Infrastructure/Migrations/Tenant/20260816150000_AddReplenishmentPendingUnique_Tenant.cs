using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Réapprovisionnement — unicité des recommandations « En attente » (fix C4). Migration ADDITIVE :
/// <list type="bullet">
///   <item>Déduplication préalable : pour chaque couple (ProductId, WarehouseId), seule la
///         recommandation Pending la plus récente est conservée ; les doublons passent à
///         Superseded (aucune ligne supprimée, historique préservé).</item>
///   <item>Index unique filtré <c>UX_ReplenishmentRecommendations_Pending_ProductWarehouse</c>
///         sur (ProductId, WarehouseId) WHERE Status = 1 (Pending) — deux générations
///         concurrentes ne peuvent plus insérer de doublon « En attente ».</item>
/// </list>
/// Les statuts sont stockés en int (Pending = 1, Superseded = 5 — convention HasConversion&lt;int&gt;
/// / défaut EF du projet). Le Down() supprime l'index mais ne restaure pas les statuts
/// dédupliqués (des doublons supersédés ne doivent pas redevenir actifs).
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260816150000_AddReplenishmentPendingUnique_Tenant")]
public partial class AddReplenishmentPendingUnique_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[ReplenishmentRecommendations]', N'U') IS NOT NULL
BEGIN
    -- 1) Déduplication : conserve la Pending la plus récente par couple (ProductId, WarehouseId).
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

    -- 2) Backstop base : une seule Pending par couple, pour toujours.
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE [name] = N'UX_ReplenishmentRecommendations_Pending_ProductWarehouse'
          AND [object_id] = OBJECT_ID(N'[dbo].[ReplenishmentRecommendations]'))
    BEGIN
        CREATE UNIQUE INDEX [UX_ReplenishmentRecommendations_Pending_ProductWarehouse]
            ON [dbo].[ReplenishmentRecommendations] ([ProductId], [WarehouseId])
            WHERE [Status] = 1;
    END
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[ReplenishmentRecommendations]', N'U') IS NOT NULL
   AND EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE [name] = N'UX_ReplenishmentRecommendations_Pending_ProductWarehouse'
          AND [object_id] = OBJECT_ID(N'[dbo].[ReplenishmentRecommendations]'))
BEGIN
    DROP INDEX [UX_ReplenishmentRecommendations_Pending_ProductWarehouse]
        ON [dbo].[ReplenishmentRecommendations];
END
-- Les lignes dédupliquées (passées à Superseded) ne sont volontairement pas restaurées.
""");
    }
}
