using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddForecastingTables_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ForecastRecomputeAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TriggerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TriggeredBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ForecastsGenerated = table.Column<int>(type: "int", nullable: false),
                    ReplenishmentsGenerated = table.Column<int>(type: "int", nullable: false),
                    PromotionsGenerated = table.Column<int>(type: "int", nullable: false),
                    ClassificationsUpdated = table.Column<int>(type: "int", nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForecastRecomputeAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductAbcXyzClassifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AbcClass = table.Column<int>(type: "int", nullable: false),
                    XyzClass = table.Column<int>(type: "int", nullable: false),
                    CumulativeRevenuePercent = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    DemandCv = table.Column<decimal>(type: "decimal(12,6)", precision: 12, scale: 6, nullable: false),
                    ReferenceRevenue = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ActiveMonths = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAbcXyzClassifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromotionRecommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    SuggestedDiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ExpectedUpliftPercent = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReasoningSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReasonCodesJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RelatedEventCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LinkedDiscountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionRecommendations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReplenishmentRecommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecommendedQty = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Rop = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SafetyStock = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: false),
                    DailyDemand = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ReasonCodesJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DismissedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LinkedPurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplenishmentRecommendations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesForecasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeType = table.Column<int>(type: "int", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Horizon = table.Column<int>(type: "int", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ExpectedCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    LowAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    LowCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    HighAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HighCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ConfidencePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MethodUsed = table.Column<int>(type: "int", nullable: false),
                    InputsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesForecasts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ForecastRecomputeAudits_StartedAt",
                table: "ForecastRecomputeAudits",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ForecastRecomputeAudits_TriggerType_StartedAt",
                table: "ForecastRecomputeAudits",
                columns: new[] { "TriggerType", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductAbcXyzClassifications_AbcClass_XyzClass",
                table: "ProductAbcXyzClassifications",
                columns: new[] { "AbcClass", "XyzClass" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductAbcXyzClassifications_ProductComputed",
                table: "ProductAbcXyzClassifications",
                columns: new[] { "ProductId", "ComputedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRecommendations_CategoryId_GeneratedAt",
                table: "PromotionRecommendations",
                columns: new[] { "CategoryId", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRecommendations_ProductId_GeneratedAt",
                table: "PromotionRecommendations",
                columns: new[] { "ProductId", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRecommendations_Status_ValidUntil",
                table: "PromotionRecommendations",
                columns: new[] { "Status", "ValidUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRecommendations_Type",
                table: "PromotionRecommendations",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_ReplenishmentRecommendations_ProductGenerated",
                table: "ReplenishmentRecommendations",
                columns: new[] { "ProductId", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReplenishmentRecommendations_Status",
                table: "ReplenishmentRecommendations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReplenishmentRecommendations_WarehouseId_Status",
                table: "ReplenishmentRecommendations",
                columns: new[] { "WarehouseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesForecasts_GeneratedAt",
                table: "SalesForecasts",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SalesForecasts_ScopeHorizonGenerated",
                table: "SalesForecasts",
                columns: new[] { "ScopeType", "ScopeId", "Horizon", "GeneratedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForecastRecomputeAudits");

            migrationBuilder.DropTable(
                name: "ProductAbcXyzClassifications");

            migrationBuilder.DropTable(
                name: "PromotionRecommendations");

            migrationBuilder.DropTable(
                name: "ReplenishmentRecommendations");

            migrationBuilder.DropTable(
                name: "SalesForecasts");
        }
    }
}
