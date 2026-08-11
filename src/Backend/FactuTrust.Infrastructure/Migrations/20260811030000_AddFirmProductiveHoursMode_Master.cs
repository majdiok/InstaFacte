using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <summary>
    /// Permet d'individualiser les heures productives d'un collaborateur sur un exercice.
    /// </summary>
    /// <remarks>
    /// Non-régression stricte : le mode est créé à 0 (<c>Parametric</c>) sur tous les exercices
    /// existants, et les dates de présence sont nulles, ce qui vaut « présent toute l'année ».
    /// Aucun taux horaire, donc aucune marge déjà calculée, ne change tant que le cabinet n'a pas
    /// explicitement basculé un exercice.
    /// </remarks>
    [DbContext(typeof(MasterDbContext))]
    [Migration("20260811030000_AddFirmProductiveHoursMode_Master")]
    public partial class AddFirmProductiveHoursMode_Master : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProductiveHoursMode",
                table: "FirmTimeSheetYearSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "HiredOn",
                table: "FirmCollaboratorProfiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeftOn",
                table: "FirmCollaboratorProfiles",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LeftOn", table: "FirmCollaboratorProfiles");
            migrationBuilder.DropColumn(name: "HiredOn", table: "FirmCollaboratorProfiles");
            migrationBuilder.DropColumn(name: "ProductiveHoursMode", table: "FirmTimeSheetYearSettings");
        }
    }
}
