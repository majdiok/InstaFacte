using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Vague 0 — correctif 7 : traçabilité des ruptures de stock.
    ///
    /// Quand le stock est insuffisant, la vente n'est pas bloquée : la sortie est limitée au
    /// stock disponible et un avertissement est écrit dans le journal applicatif. L'écart entre
    /// le vendu et le sorti restait donc invisible et irréconciliable.
    ///
    /// <c>ShortfallQuantity</c> rend cet écart mesurable, requêtable et reportable. Le
    /// COMPORTEMENT FONCTIONNEL EST INCHANGÉ : on ne bloque pas davantage, on rend visible.
    ///
    /// Colonne nullable sans valeur par défaut : les mouvements existants restent à NULL,
    /// c'est-à-dire « aucune rupture constatée », ce qui est exact — l'information n'était
    /// pas collectée.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260727140000_AddStockMovementShortfall_Tenant")]
    public partial class AddStockMovementShortfall_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ShortfallQuantity",
                table: "StockMovements",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShortfallQuantity",
                table: "StockMovements");
        }
    }
}
