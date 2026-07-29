using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Vague 1, lot 8 — code-barres article (EAN-8 / EAN-13).
    ///
    /// Le produit n'en portait aucun : le scan du point de vente cherchait dans le CODE
    /// PRODUIT interne, avec repli sur une correspondance approchée bidirectionnelle. Un scan
    /// pouvait donc encaisser un autre article — défaut de sûreté, pas seulement de confort.
    ///
    /// ⚠️ L'index est délibérément NON UNIQUE à ce stade. L'unicité par tenant ne doit être
    /// livrée qu'après un balayage des doublons sur l'ensemble du parc : une migration unique
    /// qui échoue bloquerait le tenant au démarrage (TenantMigrationGuard). Même prudence que
    /// pour l'unicité des numéros de facture — voir
    /// docs/runbooks/invoice-number-uniqueness.md.
    ///
    /// Migration additive : une colonne nullable et un index filtré.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260729100000_AddProductBarcode_Tenant")]
    public partial class AddProductBarcode_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "Products",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                filter: "[Barcode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Products_Barcode", table: "Products");
            migrationBuilder.DropColumn(name: "Barcode", table: "Products");
        }
    }
}
