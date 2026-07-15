using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Ajoute le compte 4477 « FODEC à payer » requis par GenerateInvoiceSaleEntryAsync /
    /// GenerateInvoiceCreditNoteEntryAsync lorsque la facture comporte du FODEC collecté.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260714140000_AddFodecChartAccount_Tenant")]
    public partial class AddFodecChartAccount_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM [ChartOfAccounts] WHERE [AccountNumber] = N'4477') " +
                "INSERT INTO [ChartOfAccounts] " +
                "([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], " +
                "[IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]) " +
                "VALUES ('11111111-1111-1111-1111-111111000209', N'4477', N'FODEC à payer', 4, N'447', 1, " +
                "1, 1, 4, '2026-01-01T00:00:00', NULL, N'system', NULL);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ChartOfAccounts",
                keyColumn: "AccountNumber",
                keyValue: "4477");
        }
    }
}
