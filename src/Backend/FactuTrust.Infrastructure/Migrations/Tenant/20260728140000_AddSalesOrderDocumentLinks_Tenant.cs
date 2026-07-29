using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Vague 1, lot 3 — liens de traçabilité vers la commande client.
    ///
    /// <c>DeliveryNotes.SourceSalesOrderId</c> est le lien qui fait vivre le reliquat : sans
    /// lui, le « reste à livrer » d'un bon de livraison mourait avec le bon, et rien ne
    /// permettait d'émettre un bon complémentaire rattaché au même engagement.
    ///
    /// <c>Invoices.SourceSalesOrderId</c> complète <c>SourceQuoteId</c> et
    /// <c>SourceDeliveryNoteId</c> : les trois documents amont possibles ont chacun leur lien
    /// typé. ⚠️ Il ne vaut PAS garde-fou anti-double-déduction de stock — seul
    /// <c>SourceDeliveryNoteId</c> joue ce rôle (cf. vague 0, lot B). Une facture émise
    /// directement depuis une commande n'a donné lieu à aucune sortie et doit bien déduire.
    ///
    /// Migration additive : deux colonnes nullables et deux index filtrés, aucune donnée
    /// existante touchée.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260728140000_AddSalesOrderDocumentLinks_Tenant")]
    public partial class AddSalesOrderDocumentLinks_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceSalesOrderId",
                table: "DeliveryNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceSalesOrderId",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_SourceSalesOrderId",
                table: "DeliveryNotes",
                column: "SourceSalesOrderId",
                filter: "[SourceSalesOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SourceSalesOrderId",
                table: "Invoices",
                column: "SourceSalesOrderId",
                filter: "[SourceSalesOrderId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_DeliveryNotes_SourceSalesOrderId", table: "DeliveryNotes");
            migrationBuilder.DropIndex(name: "IX_Invoices_SourceSalesOrderId", table: "Invoices");
            migrationBuilder.DropColumn(name: "SourceSalesOrderId", table: "DeliveryNotes");
            migrationBuilder.DropColumn(name: "SourceSalesOrderId", table: "Invoices");
        }
    }
}
