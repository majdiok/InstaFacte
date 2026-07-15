using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RemoveInvoiceSentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migrate existing invoices from Sent (3) to Signed (2) before removing the Sent status.
            migrationBuilder.Sql(@"
                UPDATE Invoices SET Status = 2 WHERE Status = 3
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cannot reliably restore Sent status - invoices that were Sent remain Signed.
            // This is a one-way migration for the status removal.
        }
    }
}
