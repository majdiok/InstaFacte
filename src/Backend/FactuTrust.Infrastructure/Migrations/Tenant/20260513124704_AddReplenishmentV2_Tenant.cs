using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Replenishment V2 — additive schema change:
    ///   • V2 columns on <c>Products</c> (preferred supplier, MOQ, packaging, lead-time override).
    ///   • V2 enrichment columns on <c>ReplenishmentRecommendations</c>.
    ///   • New <c>ReplenishmentDecisionAudits</c> table + supporting indexes.
    /// All columns are nullable or defaulted to 0, so existing rows remain valid.
    /// </summary>
    /// <remarks>
    /// Manual cleanup vs. <c>dotnet ef migrations add</c> output: EF also proposed to drop
    /// <c>IX_Invoices_Type</c>, an index introduced by the prior manual migration
    /// <c>20260509000000_AddInvoiceType_Tenant.cs</c> that ships without a <c>.Designer.cs</c>
    /// (hence not reflected in the snapshot). The drop is unrelated to V2 and would remove a
    /// useful index — removed from this migration. A dedicated "snapshot sync" migration is
    /// the proper place to address the drift; out of scope here.
    /// </remarks>
    public partial class AddReplenishmentV2_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DaysOfStockRemaining",
                table: "ReplenishmentRecommendations",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EffectiveQty",
                table: "ReplenishmentRecommendations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ManualQtyOverride",
                table: "ReplenishmentRecommendations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ManualSupplierOverride",
                table: "ReplenishmentRecommendations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreferredSupplierId",
                table: "ReplenishmentRecommendations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredSupplierName",
                table: "ReplenishmentRecommendations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityOnOrder",
                table: "ReplenishmentRecommendations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "UserNotes",
                table: "ReplenishmentRecommendations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeadTimeDaysOverride",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumOrderQuantity",
                table: "Products",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackagingQty",
                table: "Products",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackagingUnit",
                table: "Products",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreferredSupplierId",
                table: "Products",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReplenishmentDecisionAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecommendationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ActedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplenishmentDecisionAudits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReplenishmentRecommendations_ManualSupplier",
                table: "ReplenishmentRecommendations",
                column: "ManualSupplierOverride");

            migrationBuilder.CreateIndex(
                name: "IX_ReplenishmentRecommendations_SupplierStatus",
                table: "ReplenishmentRecommendations",
                columns: new[] { "PreferredSupplierId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReplenishmentDecisionAudits_ActedAt",
                table: "ReplenishmentDecisionAudits",
                column: "ActedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReplenishmentDecisionAudits_RecommendationActed",
                table: "ReplenishmentDecisionAudits",
                columns: new[] { "RecommendationId", "ActedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReplenishmentDecisionAudits");

            migrationBuilder.DropIndex(
                name: "IX_ReplenishmentRecommendations_ManualSupplier",
                table: "ReplenishmentRecommendations");

            migrationBuilder.DropIndex(
                name: "IX_ReplenishmentRecommendations_SupplierStatus",
                table: "ReplenishmentRecommendations");

            migrationBuilder.DropColumn(
                name: "DaysOfStockRemaining",
                table: "ReplenishmentRecommendations");

            migrationBuilder.DropColumn(
                name: "EffectiveQty",
                table: "ReplenishmentRecommendations");

            migrationBuilder.DropColumn(
                name: "ManualQtyOverride",
                table: "ReplenishmentRecommendations");

            migrationBuilder.DropColumn(
                name: "ManualSupplierOverride",
                table: "ReplenishmentRecommendations");

            migrationBuilder.DropColumn(
                name: "PreferredSupplierId",
                table: "ReplenishmentRecommendations");

            migrationBuilder.DropColumn(
                name: "PreferredSupplierName",
                table: "ReplenishmentRecommendations");

            migrationBuilder.DropColumn(
                name: "QuantityOnOrder",
                table: "ReplenishmentRecommendations");

            migrationBuilder.DropColumn(
                name: "UserNotes",
                table: "ReplenishmentRecommendations");

            migrationBuilder.DropColumn(
                name: "LeadTimeDaysOverride",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MinimumOrderQuantity",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PackagingQty",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PackagingUnit",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PreferredSupplierId",
                table: "Products");
        }
    }
}
