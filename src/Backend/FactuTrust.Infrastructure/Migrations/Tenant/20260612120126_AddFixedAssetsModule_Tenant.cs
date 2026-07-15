using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddFixedAssetsModule_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DepreciationRateCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    LegalRatePercent = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    DefaultAssetAccount = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DefaultDepreciationAccount = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DefaultExpenseAccount = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsNonDepreciable = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepreciationRateCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FixedAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AssetAccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DepreciationAccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExpenseAccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AcquisitionCost = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CapitalizedFees = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ResidualValue = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    VatAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AcquisitionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InServiceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisposalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DepreciationRateCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepreciationRatePercent = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    UsefulLifeYears = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    DepreciationMethod = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AccumulatedDepreciation = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NetBookValue = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreditAccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DisposalProceeds = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    DisposalTreasuryAccount = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixedAssets_DepreciationRateCategories_DepreciationRateCategoryId",
                        column: x => x.DepreciationRateCategoryId,
                        principalTable: "DepreciationRateCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DepreciationScheduleLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FixedAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    PeriodMonth = table.Column<int>(type: "int", nullable: true),
                    OpeningNbv = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NormalAnnualAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PriorAccumulatedDepreciation = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DepreciationAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AccumulatedDepreciation = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ClosingNbv = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    IsPosted = table.Column<bool>(type: "bit", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccountingPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepreciationScheduleLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepreciationScheduleLines_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FixedAssetEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FixedAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    EventDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedAssetEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixedAssetEvents_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationRateCategories_Code",
                table: "DepreciationRateCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationRateCategories_SortOrder",
                table: "DepreciationRateCategories",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationScheduleLines_FixedAssetId_FiscalYear_PeriodMonth",
                table: "DepreciationScheduleLines",
                columns: new[] { "FixedAssetId", "FiscalYear", "PeriodMonth" },
                unique: true,
                filter: "[PeriodMonth] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationScheduleLines_IsPosted",
                table: "DepreciationScheduleLines",
                column: "IsPosted");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetEvents_EventDate",
                table: "FixedAssetEvents",
                column: "EventDate");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetEvents_FixedAssetId",
                table: "FixedAssetEvents",
                column: "FixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_DepreciationRateCategoryId",
                table: "FixedAssets",
                column: "DepreciationRateCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_InventoryNumber",
                table: "FixedAssets",
                column: "InventoryNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_Status",
                table: "FixedAssets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationScheduleLines_FixedAssetId_FiscalYear_Annual",
                table: "DepreciationScheduleLines",
                columns: new[] { "FixedAssetId", "FiscalYear" },
                unique: true,
                filter: "[PeriodMonth] IS NULL");

            SeedDepreciationRateCategories(migrationBuilder);
            SeedFixedAssetChartAccounts(migrationBuilder);
        }

        private static void SeedDepreciationRateCategories(MigrationBuilder migrationBuilder)
        {
            void Cat(string id, string code, string label, decimal rate, string asset, string amort, string expense, bool nonDep, int sort)
            {
                var safeLabel = label.Replace("'", "''");
                migrationBuilder.Sql(
                    $"IF NOT EXISTS (SELECT 1 FROM [DepreciationRateCategories] WHERE [Code] = N'{code}') " +
                    "INSERT INTO [DepreciationRateCategories] " +
                    "([Id], [Code], [Label], [LegalRatePercent], [DefaultAssetAccount], [DefaultDepreciationAccount], " +
                    "[DefaultExpenseAccount], [IsNonDepreciable], [SortOrder], [IsActive], [CreatedAt], [CreatedBy]) " +
                    $"VALUES ('{id}', N'{code}', N'{safeLabel}', {rate.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
                    $"N'{asset}', N'{amort}', N'{expense}', {(nonDep ? 1 : 0)}, {sort}, 1, '2026-06-12T00:00:00', N'system');");
            }

            Cat("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001", "LAND", "Terrains", 0m, "211", "2811", "6811", true, 1);
            Cat("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0002", "PRELIM", "Frais préliminaires et charges à répartir", 100m, "20", "280", "681", false, 2);
            Cat("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0003", "PATENT", "Brevets, licences, marques, logiciels", 20m, "20", "280", "681", false, 3);
            Cat("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0004", "BLDG_PERM", "Constructions permanentes", 5m, "212", "2812", "6812", false, 10);
            Cat("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0005", "BLDG_LIGHT", "Constructions légères et installations", 10m, "212", "2812", "6812", false, 11);
            Cat("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0006", "BLDG_TEMP", "Constructions temporaires", 25m, "212", "2812", "6812", false, 12);
            Cat("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0007", "TECH_INST", "Installations techniques, matériel industriel", 15m, "213", "2813", "6813", false, 20);
            Cat("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0008", "MACH_GEN", "Machines et équipements généraux", 15m, "213", "2813", "6813", false, 21);
            Cat("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0009", "MACH_AGRI", "Matériel agricole", 20m, "213", "2813", "6813", false, 22);
            Cat("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0010", "IT_EQUIP", "Matériel informatique", 33.33m, "218", "2818", "6818", false, 30);
            Cat("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0011", "OFF_FURN", "Mobilier de bureau", 20m, "218", "2818", "6818", false, 31);
            Cat("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0012", "VEH_UTIL", "Véhicules de transport utilitaires", 20m, "218", "2818", "6818", false, 32);
            Cat("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0013", "VEH_PASS", "Véhicules de tourisme", 3.33m, "218", "2818", "6818", false, 33);
            Cat("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0014", "AGRI_TREE", "Plantations et arbres fruitiers", 3.33m, "211", "2811", "6811", false, 40);
            Cat("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0015", "AGRI_IRR", "Équipements d'irrigation", 20m, "213", "2813", "6813", false, 41);
            Cat("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0016", "OTHER", "Autres immobilisations corporelles", 15m, "218", "2818", "6818", false, 99);
        }

        private static void SeedFixedAssetChartAccounts(MigrationBuilder migrationBuilder)
        {
            void Row(string id, string num, string label, int cls, string parent, int nature, int level)
            {
                var safeLabel = label.Replace("'", "''");
                var parentSql = string.IsNullOrEmpty(parent) ? "NULL" : $"N'{parent}'";
                migrationBuilder.Sql(
                    $"IF NOT EXISTS (SELECT 1 FROM [ChartOfAccounts] WHERE [AccountNumber] = N'{num}') " +
                    "INSERT INTO [ChartOfAccounts] " +
                    "([Id], [AccountNumber], [Label], [AccountClass], [ParentAccountNumber], [NatureType], " +
                    "[IsSystem], [IsActive], [Level], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy]) " +
                    $"VALUES ('{id}', N'{num}', N'{safeLabel}', {cls}, {parentSql}, {nature}, " +
                    $"1, 1, {level}, '2026-06-12T00:00:00', NULL, N'system', NULL);");
            }

            Row("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0001", "68", "Dotations aux amortissements et aux provisions", 6, "", 0, 2);
            Row("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0002", "681", "Dotations aux amortissements sur immobilisations", 6, "68", 0, 3);
            Row("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0003", "6811", "Dotations - immobilisations incorporelles", 6, "681", 0, 4);
            Row("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0004", "6812", "Dotations - constructions", 6, "681", 0, 4);
            Row("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0005", "6813", "Dotations - installations techniques", 6, "681", 0, 4);
            Row("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0006", "6818", "Dotations - autres immobilisations corporelles", 6, "681", 0, 4);
            Row("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0007", "2811", "Amortissements - terrains et incorporelles", 2, "281", 0, 4);
            Row("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0008", "2812", "Amortissements - constructions", 2, "281", 0, 4);
            Row("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0009", "2813", "Amortissements - installations techniques", 2, "281", 0, 4);
            Row("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0010", "2818", "Amortissements - autres immobilisations corporelles", 2, "281", 0, 4);
            Row("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0011", "67", "Charges exceptionnelles", 6, "", 0, 2);
            Row("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0012", "675", "Valeurs comptables des éléments d'actif cédés", 6, "67", 0, 3);
            Row("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0013", "78", "Reprises sur amortissements et provisions", 7, "", 1, 2);
            Row("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0014", "781", "Reprises sur amortissements des immobilisations", 7, "78", 1, 3);
            Row("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0015", "280", "Amortissements des immobilisations incorporelles", 2, "28", 0, 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepreciationScheduleLines");

            migrationBuilder.DropTable(
                name: "FixedAssetEvents");

            migrationBuilder.DropTable(
                name: "FixedAssets");

            migrationBuilder.DropTable(
                name: "DepreciationRateCategories");
        }
    }
}
