using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Vague 0 — correctif 2 : FODEC et timbre fiscal sur le devis.
    ///
    /// Le devis ne portait ni FODEC ni timbre alors que la facture applique les deux : le client
    /// recevait donc systématiquement une facture supérieure au devis qu'il avait accepté
    /// (1 % de FODEC sur les produits assujettis, plus le timbre fiscal).
    ///
    /// Migration strictement additive, SANS recalcul rétroactif : les colonnes naissent à zéro
    /// et aucun devis existant n'est modifié — on ne mute jamais un document déjà émis.
    /// Les devis reprendront le calcul complet à leur prochain enregistrement.
    ///
    /// Durcissement inclus : la migration FODEC de juillet (20260713171729) avait créé les
    /// colonnes de devise avec defaultValue "" sans backfill. Une devise vide fait échouer
    /// toute opération Money.Add. On la normalise ici à 'TND' sur les factures et leurs lignes.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260727120000_AddQuoteFodecAndFiscalStamp_Tenant")]
    public partial class AddQuoteFodecAndFiscalStamp_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─── En-tête du devis : FODEC agrégé + timbre fiscal ───
            migrationBuilder.AddColumn<decimal>(
                name: "FodecAmount",
                table: "Quotes",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "FodecAmountCurrency",
                table: "Quotes",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "TND");

            migrationBuilder.AddColumn<decimal>(
                name: "FiscalStampAmount",
                table: "Quotes",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "FiscalStampAmountCurrency",
                table: "Quotes",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "TND");

            // ─── Lignes du devis : FODEC ───
            migrationBuilder.AddColumn<decimal>(
                name: "FodecAmount",
                table: "QuoteLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "FodecAmountCurrency",
                table: "QuoteLines",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "TND");

            migrationBuilder.AddColumn<bool>(
                name: "IsFodecApplicable",
                table: "QuoteLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "FodecRatePercent",
                table: "QuoteLines",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 1.0m);

            // ─── Durcissement : devises FODEC vides héritées de 20260713171729 ───
            migrationBuilder.Sql("""
                UPDATE Invoices     SET FodecAmountCurrency = 'TND' WHERE ISNULL(FodecAmountCurrency, '') = '';
                UPDATE InvoiceLines SET FodecAmountCurrency = 'TND' WHERE ISNULL(FodecAmountCurrency, '') = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "FodecAmount", table: "Quotes");
            migrationBuilder.DropColumn(name: "FodecAmountCurrency", table: "Quotes");
            migrationBuilder.DropColumn(name: "FiscalStampAmount", table: "Quotes");
            migrationBuilder.DropColumn(name: "FiscalStampAmountCurrency", table: "Quotes");

            migrationBuilder.DropColumn(name: "FodecAmount", table: "QuoteLines");
            migrationBuilder.DropColumn(name: "FodecAmountCurrency", table: "QuoteLines");
            migrationBuilder.DropColumn(name: "IsFodecApplicable", table: "QuoteLines");
            migrationBuilder.DropColumn(name: "FodecRatePercent", table: "QuoteLines");
        }
    }
}
