using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class BackfillPaymentsForPaidInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill Payment records for invoices that were marked as Paid (Status=4) with PaidAt set
            // but have no corresponding Payment records. This ensures data consistency for the new
            // multi-payment module.
            migrationBuilder.Sql(@"
                INSERT INTO Payments (Id, InvoiceId, Amount, Currency, PaymentDate, Method, Reference, Notes, IsRefunded, RefundedAt, RefundReason, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, Version)
                SELECT
                    NEWID(),
                    i.Id,
                    i.TotalAmount,
                    ISNULL(i.TotalAmountCurrency, 'TND'),
                    i.PaidAt,
                    99,
                    NULL,
                    'Migration: paiement historique avant support multi-paiements',
                    0,
                    NULL,
                    NULL,
                    i.PaidAt,
                    NULL,
                    i.UpdatedBy,
                    NULL,
                    0
                FROM Invoices i
                WHERE i.Status = 4
                  AND i.PaidAt IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM Payments p WHERE p.InvoiceId = i.Id)
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove payments that were created by this migration (identified by the notes)
            migrationBuilder.Sql(@"
                DELETE FROM Payments
                WHERE Notes = 'Migration: paiement historique avant support multi-paiements'
            ");
        }
    }
}
