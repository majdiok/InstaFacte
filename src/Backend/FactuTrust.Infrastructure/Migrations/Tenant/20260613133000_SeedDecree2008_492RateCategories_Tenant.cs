using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Seed additif : rubriques complementaires du Decret 2008-492 (taux d'amortissement).
    /// Les 16 categories initiales ne sont pas modifiees.
    /// </summary>
    public partial class SeedDecree2008_492RateCategories_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            SeedDecree2008_492RateCategories(migrationBuilder);
        }

        private static void SeedDecree2008_492RateCategories(MigrationBuilder migrationBuilder)
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
                    $"N'{asset}', N'{amort}', N'{expense}', {(nonDep ? 1 : 0)}, {sort}, 1, '2026-06-13T00:00:00', N'system');");
            }

            Cat("cccccccc-cccc-cccc-cccc-cccccccc0001", "BLDG_BRIDGE", "Ponts et ouvrages d'art", 2.5m, "212", "2812", "6812", false, 13);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0002", "BLDG_DUR", "Constructions durables", 5m, "212", "2812", "6812", false, 14);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0010", "MAJOR_REP_3", "Grosses reparations - duree <= 3 ans", 33.33m, "213", "2813", "6813", false, 23);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0011", "MAJOR_REP_4", "Grosses reparations - duree 4 ans", 25m, "213", "2813", "6813", false, 24);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0012", "MAJOR_REP_6", "Grosses reparations - duree 6 ans", 15m, "213", "2813", "6813", false, 25);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0013", "MAJOR_REP_8", "Grosses reparations - duree 8 ans", 12.5m, "213", "2813", "6813", false, 26);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0020", "SHELVING", "Rayonnages et etageres industrielles", 15m, "218", "2818", "6818", false, 34);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0021", "CONTAINER", "Conteneurs et emballages reutilisables", 10m, "218", "2818", "6818", false, 35);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0022", "TANK", "Citernes et reservoirs", 20m, "218", "2818", "6818", false, 36);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0030", "RAIL_20", "Materiel ferroviaire - duree 20 ans", 5m, "218", "2818", "6818", false, 50);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0031", "RAIL_30", "Materiel ferroviaire - duree 30 ans", 3.33m, "218", "2818", "6818", false, 51);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0032", "RAIL_15", "Materiel ferroviaire - duree 15 ans", 6.67m, "218", "2818", "6818", false, 52);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0033", "AIR_TRANS", "Materiel de transport aerien", 5.56m, "218", "2818", "6818", false, 53);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0034", "SEA_TRANS", "Materiel de transport maritime", 6.25m, "218", "2818", "6818", false, 54);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0035", "ROAD_TRANS", "Materiel de transport terrestre", 20m, "218", "2818", "6818", false, 55);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0036", "PUB_WORKS", "Materiel de travaux publics", 20m, "218", "2818", "6818", false, 56);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0040", "UTIL_GAS", "Installations electricite et gaz", 5m, "213", "2813", "6813", false, 60);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0050", "HOTEL_KITCH", "Materiel de cuisine (hotellerie)", 20m, "218", "2818", "6818", false, 70);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0051", "HOTEL_DISH", "Vaisselle et petit materiel (hotellerie)", 100m, "218", "2818", "6818", false, 71);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0052", "HOTEL_LINEN", "Linge et literie (hotellerie)", 33.33m, "218", "2818", "6818", false, 72);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0060", "AGRI_TRACT", "Tracteurs et materiel de traction", 20m, "213", "2813", "6813", false, 42);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0061", "AGRI_OLIVE_30", "Oliviers - duree 30 ans", 3.33m, "211", "2811", "6811", false, 43);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0062", "AGRI_OLIVE_20", "Oliviers - duree 20 ans", 5m, "211", "2811", "6811", false, 44);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0063", "AGRI_VINE", "Vignes", 3.33m, "211", "2811", "6811", false, 45);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0064", "AGRI_CITRUS", "Agrumes et amandiers", 5m, "211", "2811", "6811", false, 46);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0065", "AGRI_WELL", "Puits et forages", 10m, "213", "2813", "6813", false, 47);
            Cat("cccccccc-cccc-cccc-cccc-cccccccc0066", "AGRI_IRRIG", "Installations d'arrosage", 20m, "213", "2813", "6813", false, 48);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM [DepreciationRateCategories]
                WHERE [Code] IN (
                    N'BLDG_BRIDGE', N'BLDG_DUR',
                    N'MAJOR_REP_3', N'MAJOR_REP_4', N'MAJOR_REP_6', N'MAJOR_REP_8',
                    N'SHELVING', N'CONTAINER', N'TANK',
                    N'RAIL_20', N'RAIL_30', N'RAIL_15', N'AIR_TRANS', N'SEA_TRANS', N'ROAD_TRANS', N'PUB_WORKS',
                    N'UTIL_GAS',
                    N'HOTEL_KITCH', N'HOTEL_DISH', N'HOTEL_LINEN',
                    N'AGRI_TRACT', N'AGRI_OLIVE_30', N'AGRI_OLIVE_20', N'AGRI_VINE', N'AGRI_CITRUS', N'AGRI_WELL', N'AGRI_IRRIG'
                );");
        }
    }
}