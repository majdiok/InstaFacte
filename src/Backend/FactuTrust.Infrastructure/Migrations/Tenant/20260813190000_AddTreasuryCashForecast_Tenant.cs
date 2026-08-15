using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Trésorerie prévisionnelle par IA : projections, flux attendus, agrégats mensuels,
    /// scénarios probabilisés, analyses, engagements récurrents et seuils de la jauge.
    /// </summary>
    /// <remarks>
    /// Strictement additive : aucune table ni colonne existante n'est touchée. Les tables ne sont
    /// alimentées que lorsque <c>TreasuryForecast:Enabled</c> vaut true — appliquer cette migration
    /// sur un tenant dont le module est éteint est donc sans effet fonctionnel.
    /// Écrite à la main (comme toutes les migrations tenant depuis 2026-08) : le snapshot EF du
    /// contexte tenant est désynchronisé de l'historique, et une génération automatique
    /// embarquerait l'arriéré des autres modules.
    /// </remarks>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260813190000_AddTreasuryCashForecast_Tenant")]
    public partial class AddTreasuryCashForecast_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashFlowForecastRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HorizonMonths = table.Column<int>(type: "int", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalInflows = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalOutflows = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NetFlow = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ConfidencePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MethodUsed = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ComputedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    InputsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AiAdjustmentApplied = table.Column<bool>(type: "bit", nullable: false),
                    AiModelRef = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AiAnalyzedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashFlowForecastRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashFlowForecastLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ForecastRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ThirdPartyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContractualDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ProbabilityPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    WeightedAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    IsConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashFlowForecastLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashFlowForecastLines_CashFlowForecastRuns_ForecastRunId",
                        column: x => x.ForecastRunId,
                        principalTable: "CashFlowForecastRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CashFlowForecastBuckets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ForecastRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SequenceIndex = table.Column<int>(type: "int", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Inflows = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Outflows = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NetFlow = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    LowClosingBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HighClosingBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashFlowForecastBuckets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashFlowForecastBuckets_CashFlowForecastRuns_ForecastRunId",
                        column: x => x.ForecastRunId,
                        principalTable: "CashFlowForecastRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CashFlowScenarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ForecastRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NetFlow = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DeterministicProbabilityPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ProbabilityPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ProbabilitySource = table.Column<int>(type: "int", nullable: false),
                    AiRationale = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AssumptionsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashFlowScenarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashFlowScenarios_CashFlowForecastRuns_ForecastRunId",
                        column: x => x.ForecastRunId,
                        principalTable: "CashFlowForecastRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CashFlowForecastInsights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ForecastRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Origin = table.Column<int>(type: "int", nullable: false),
                    Impact = table.Column<int>(type: "int", nullable: true),
                    ImpactDirection = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstimatedBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashFlowForecastInsights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashFlowForecastInsights_CashFlowForecastRuns_ForecastRunId",
                        column: x => x.ForecastRunId,
                        principalTable: "CashFlowForecastRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecurringCashCommitments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    DayOfMonth = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringCashCommitments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashFlowForecastSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriticalThreshold = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AlertThreshold = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ComfortThreshold = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PayrollPaymentDayOfMonth = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashFlowForecastSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowForecastRuns_StatusComputed",
                table: "CashFlowForecastRuns",
                columns: new[] { "Status", "ComputedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowForecastRuns_ComputedAt",
                table: "CashFlowForecastRuns",
                column: "ComputedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowForecastLines_RunExpectedDate",
                table: "CashFlowForecastLines",
                columns: new[] { "ForecastRunId", "ExpectedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowForecastLines_RunDirection",
                table: "CashFlowForecastLines",
                columns: new[] { "ForecastRunId", "Direction" });

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowForecastLines_Source",
                table: "CashFlowForecastLines",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowForecastBuckets_RunSequence",
                table: "CashFlowForecastBuckets",
                columns: new[] { "ForecastRunId", "SequenceIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowScenarios_RunKind",
                table: "CashFlowScenarios",
                columns: new[] { "ForecastRunId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowForecastInsights_RunKindSort",
                table: "CashFlowForecastInsights",
                columns: new[] { "ForecastRunId", "Kind", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringCashCommitments_ActiveStart",
                table: "RecurringCashCommitments",
                columns: new[] { "IsActive", "StartDate" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Les enfants d'abord : leurs clés étrangères pointent sur CashFlowForecastRuns.
            migrationBuilder.DropTable(name: "CashFlowForecastLines");
            migrationBuilder.DropTable(name: "CashFlowForecastBuckets");
            migrationBuilder.DropTable(name: "CashFlowScenarios");
            migrationBuilder.DropTable(name: "CashFlowForecastInsights");
            migrationBuilder.DropTable(name: "CashFlowForecastRuns");
            migrationBuilder.DropTable(name: "RecurringCashCommitments");
            migrationBuilder.DropTable(name: "CashFlowForecastSettings");
        }
    }
}
