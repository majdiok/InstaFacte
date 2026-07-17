using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddInvoiceSearchAndAuditIndexes_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Number",
                table: "Invoices",
                column: "Number");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_NumberYear_NumberPrefix_NumberSequence",
                table: "Invoices",
                columns: new[] { "NumberYear", "NumberPrefix", "NumberSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt_Id",
                table: "AuditLogs",
                columns: new[] { "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_Number",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_NumberYear_NumberPrefix_NumberSequence",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_CreatedAt_Id",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");
        }
    }
}
