using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Vague 0 — correctif 4 : lien avoir → facture d'origine.
    ///
    /// <c>LinkedInvoiceId</c> ne vivait que dans le JSON des métadonnées du brouillon
    /// (<c>InvoiceDrafts.MetadataJson</c>) et n'était jamais reporté sur l'agrégat Invoice à la
    /// soumission. Après conversion, une facture d'avoir n'avait donc plus aucun lien structuré
    /// vers la facture qu'elle rectifie, et son PDF n'en portait aucune référence.
    ///
    /// La colonne reste nullable : les avoirs historiques ne sont pas invalidés. L'obligation
    /// est portée par la fabrique <c>Invoice.CreateCreditNote</c>, donc appliquée aux nouveaux
    /// avoirs uniquement.
    ///
    /// Backfill best-effort depuis les brouillons convertis : le lien est extrait du JSON par
    /// JSON_VALUE. Un échec d'extraction est sans gravité — l'information est informative, pas
    /// structurante pour les traitements existants.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260727130000_AddInvoiceLinkedInvoice_Tenant")]
    public partial class AddInvoiceLinkedInvoice_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LinkedInvoiceId",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: true);

            // Backfill best-effort : brouillons convertis portant un LinkedInvoiceId exploitable.
            migrationBuilder.Sql("""
                UPDATE i
                SET i.LinkedInvoiceId = TRY_CONVERT(uniqueidentifier, JSON_VALUE(d.MetadataJson, '$.LinkedInvoiceId'))
                FROM Invoices i
                INNER JOIN InvoiceDrafts d ON d.ConvertedInvoiceId = i.Id
                WHERE i.LinkedInvoiceId IS NULL
                  AND i.Type = 1
                  AND d.MetadataJson IS NOT NULL
                  AND ISJSON(d.MetadataJson) = 1
                  AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(d.MetadataJson, '$.LinkedInvoiceId')) IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_LinkedInvoiceId",
                table: "Invoices",
                column: "LinkedInvoiceId",
                filter: "[LinkedInvoiceId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_LinkedInvoiceId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "LinkedInvoiceId",
                table: "Invoices");
        }
    }
}
