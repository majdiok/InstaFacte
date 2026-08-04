using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Traçabilité des promotions appliquées automatiquement sur les lignes documentaires.
    /// Migration additive : colonnes nullables, aucun impact sur l'existant.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260801180000_AddAppliedPromotionToDocumentLines_Tenant")]
    public partial class AddAppliedPromotionToDocumentLines_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddPromotionColumns(migrationBuilder, "QuoteLines");
            AddPromotionColumns(migrationBuilder, "SalesOrderLines");
            AddPromotionColumns(migrationBuilder, "InvoiceLines");
            AddPromotionColumns(migrationBuilder, "DeliveryNoteLines");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropPromotionColumns(migrationBuilder, "QuoteLines");
            DropPromotionColumns(migrationBuilder, "SalesOrderLines");
            DropPromotionColumns(migrationBuilder, "InvoiceLines");
            DropPromotionColumns(migrationBuilder, "DeliveryNoteLines");
        }

        private static void AddPromotionColumns(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AppliedPromotionId",
                table: table,
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppliedPromotionName",
                table: table,
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        private static void DropPromotionColumns(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.DropColumn(name: "AppliedPromotionId", table: table);
            migrationBuilder.DropColumn(name: "AppliedPromotionName", table: table);
        }
    }
}
