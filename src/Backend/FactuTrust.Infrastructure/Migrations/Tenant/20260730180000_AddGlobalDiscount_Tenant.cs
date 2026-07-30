using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Vague 1, lot 5 tranche 5B — remise de pied de document sur devis, commande et facture.
    ///
    /// Strictement additive. Toutes les colonnes de montant sont créées à 0 et le pourcentage à
    /// NULL : sur l'existant, la part de remise imputée à chaque ligne vaut zéro, et le calcul
    /// (remise de ligne → FODEC → TVA) reste identique au millime près.
    ///
    /// La remise est répartie sur les lignes au prorata de leur base HT, de sorte que le FODEC
    /// et la base de TVA portent sur ce qui est réellement facturé.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260730180000_AddGlobalDiscount_Tenant")]
    public partial class AddGlobalDiscount_Tenant : Migration
    {
        private static readonly string[] DocumentTables = { "Invoices", "Quotes", "SalesOrders" };
        private static readonly string[] LineTables = { "InvoiceLines", "QuoteLines", "SalesOrderLines" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in DocumentTables)
            {
                migrationBuilder.AddColumn<decimal>(
                    name: "GlobalDiscountPercent",
                    table: table,
                    type: "decimal(5,2)",
                    precision: 5,
                    scale: 2,
                    nullable: true);

                migrationBuilder.AddColumn<decimal>(
                    name: "GlobalDiscountAmount",
                    table: table,
                    type: "decimal(18,3)",
                    precision: 18,
                    scale: 3,
                    nullable: false,
                    defaultValue: 0m);

                migrationBuilder.AddColumn<string>(
                    name: "GlobalDiscountAmountCurrency",
                    table: table,
                    type: "nvarchar(3)",
                    maxLength: 3,
                    nullable: false,
                    defaultValue: "TND");
            }

            foreach (var table in LineTables)
            {
                migrationBuilder.AddColumn<decimal>(
                    name: "AllocatedGlobalDiscount",
                    table: table,
                    type: "decimal(18,3)",
                    precision: 18,
                    scale: 3,
                    nullable: false,
                    defaultValue: 0m);

                migrationBuilder.AddColumn<string>(
                    name: "AllocatedGlobalDiscountCurrency",
                    table: table,
                    type: "nvarchar(3)",
                    maxLength: 3,
                    nullable: false,
                    defaultValue: "TND");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in LineTables)
            {
                migrationBuilder.DropColumn(name: "AllocatedGlobalDiscountCurrency", table: table);
                migrationBuilder.DropColumn(name: "AllocatedGlobalDiscount", table: table);
            }

            foreach (var table in DocumentTables)
            {
                migrationBuilder.DropColumn(name: "GlobalDiscountAmountCurrency", table: table);
                migrationBuilder.DropColumn(name: "GlobalDiscountAmount", table: table);
                migrationBuilder.DropColumn(name: "GlobalDiscountPercent", table: table);
            }
        }
    }
}
