using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Vague 0 — correctif 1 : lien typé bon de livraison → facture.
    ///
    /// Le garde-fou anti-double-déduction de stock reposait sur un préfixe de chaîne dans
    /// <c>Invoices.Reference</c>, champ libre saisissable par l'appelant de
    /// <c>POST /api/delivery-notes/{id}/generate-invoice</c>. Une référence personnalisée
    /// décrémentait le stock deux fois ; une facture directe intitulée « BL … » ne le
    /// décrémentait jamais.
    ///
    /// La colonne est rétro-alimentée depuis <c>DeliveryNotes.InvoiceId</c>, relation inverse
    /// déjà persistée et fiable, ce qui rend le backfill exhaustif sans heuristique de texte.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260727100000_AddInvoiceSourceDeliveryNote_Tenant")]
    public partial class AddInvoiceSourceDeliveryNote_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceDeliveryNoteId",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: true);

            // Backfill depuis la relation inverse existante (BL → facture).
            migrationBuilder.Sql("""
                UPDATE i
                SET i.SourceDeliveryNoteId = dn.Id
                FROM Invoices i
                INNER JOIN DeliveryNotes dn ON dn.InvoiceId = i.Id
                WHERE i.SourceDeliveryNoteId IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SourceDeliveryNoteId",
                table: "Invoices",
                column: "SourceDeliveryNoteId",
                filter: "[SourceDeliveryNoteId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_SourceDeliveryNoteId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SourceDeliveryNoteId",
                table: "Invoices");
        }
    }
}
