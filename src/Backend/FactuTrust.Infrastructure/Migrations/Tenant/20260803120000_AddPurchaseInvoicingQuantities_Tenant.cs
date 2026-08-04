using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Partial invoicing: InvoicedQuantity on PO/BR lines, source links on supplier invoices.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260803120000_AddPurchaseInvoicingQuantities_Tenant")]
    public partial class AddPurchaseInvoicingQuantities_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InvoicedQuantity",
                table: "PurchaseOrderLines",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InvoicedQuantity",
                table: "PurchaseReceiptLines",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "SourcePurchaseReceiptId",
                table: "SupplierInvoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseOrderLineId",
                table: "SupplierInvoiceLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseReceiptLineId",
                table: "SupplierInvoiceLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoices_SourcePurchaseReceiptId",
                table: "SupplierInvoices",
                column: "SourcePurchaseReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoiceLines_PurchaseOrderLineId",
                table: "SupplierInvoiceLines",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoiceLines_PurchaseReceiptLineId",
                table: "SupplierInvoiceLines",
                column: "PurchaseReceiptLineId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierInvoices_PurchaseReceipts_SourcePurchaseReceiptId",
                table: "SupplierInvoices",
                column: "SourcePurchaseReceiptId",
                principalTable: "PurchaseReceipts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Backfill: existing non-cancelled invoices on Invoiced POs
            migrationBuilder.Sql(@"
                UPDATE pol
                SET pol.InvoicedQuantity = pol.ReceivedQuantity
                FROM PurchaseOrderLines pol
                INNER JOIN PurchaseOrders po ON po.Id = pol.PurchaseOrderId
                INNER JOIN SupplierInvoices si ON si.PurchaseOrderId = po.Id
                WHERE po.Status = 5 AND si.Status <> 2 AND pol.ReceivedQuantity > 0;

                UPDATE sil
                SET sil.PurchaseOrderLineId = pol.Id
                FROM SupplierInvoiceLines sil
                INNER JOIN SupplierInvoices si ON si.Id = sil.SupplierInvoiceId
                INNER JOIN PurchaseOrders po ON po.Id = si.PurchaseOrderId
                INNER JOIN PurchaseOrderLines pol ON pol.PurchaseOrderId = po.Id AND pol.ProductId = sil.ProductId
                WHERE sil.PurchaseOrderLineId IS NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierInvoices_PurchaseReceipts_SourcePurchaseReceiptId",
                table: "SupplierInvoices");

            migrationBuilder.DropIndex(
                name: "IX_SupplierInvoices_SourcePurchaseReceiptId",
                table: "SupplierInvoices");

            migrationBuilder.DropIndex(
                name: "IX_SupplierInvoiceLines_PurchaseOrderLineId",
                table: "SupplierInvoiceLines");

            migrationBuilder.DropIndex(
                name: "IX_SupplierInvoiceLines_PurchaseReceiptLineId",
                table: "SupplierInvoiceLines");

            migrationBuilder.DropColumn(name: "InvoicedQuantity", table: "PurchaseOrderLines");
            migrationBuilder.DropColumn(name: "InvoicedQuantity", table: "PurchaseReceiptLines");
            migrationBuilder.DropColumn(name: "SourcePurchaseReceiptId", table: "SupplierInvoices");
            migrationBuilder.DropColumn(name: "PurchaseOrderLineId", table: "SupplierInvoiceLines");
            migrationBuilder.DropColumn(name: "PurchaseReceiptLineId", table: "SupplierInvoiceLines");
        }
    }
}
