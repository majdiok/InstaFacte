using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <summary>
    /// Taux CSS patronale de repli pour la gouvernance des temps (coût horaire de revient).
    /// Défaut 0 pour préserver les taux patronaux existants (19,57 %).
    /// </summary>
    [DbContext(typeof(MasterDbContext))]
    [Migration("20260807120000_AddFirmCssEmployerRate_Master")]
    public partial class AddFirmCssEmployerRate_Master : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CssEmployerRate",
                table: "FirmTimeSheetYearSettings",
                type: "decimal(9,3)",
                precision: 9,
                scale: 3,
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CssEmployerRate",
                table: "FirmTimeSheetYearSettings");
        }
    }
}
