using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFirmRentabilityYearConfig_Master : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// ATTENTION — opération destructive et irréversible.
        ///
        /// La suppression de FirmRentabilityYearConfigs efface les paramètres de charges saisis par
        /// les cabinets. Le Down recrée la structure de la table mais ne restaure AUCUNE donnée.
        ///
        /// À exécuter et à archiver AVANT d'appliquer cette migration :
        ///   sqlcmd -S &lt;serveur&gt; -d &lt;base_master&gt; ^
        ///     -Q "SET NOCOUNT ON; SELECT * FROM FirmRentabilityYearConfigs FOR JSON PATH" ^
        ///     -o config-rentabilite-sauvegarde.json
        ///
        /// Les deux colonnes ajoutées conservent la marge d'origine de chaque snapshot lors du
        /// recalcul déclenché depuis l'écran de rentabilité collaborateurs.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FirmRentabilityYearConfigs");

            migrationBuilder.AddColumn<decimal>(
                name: "LegacyRentability",
                table: "FirmCollaboratorRentabilities",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecalculatedAt",
                table: "FirmCollaboratorRentabilities",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LegacyRentability",
                table: "FirmCollaboratorRentabilities");

            migrationBuilder.DropColumn(
                name: "RecalculatedAt",
                table: "FirmCollaboratorRentabilities");

            migrationBuilder.CreateTable(
                name: "FirmRentabilityYearConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirmTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GlobalItCharges = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    GlobalOperatingCharges = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    GlobalPayrollMass = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalManagers = table.Column<int>(type: "int", nullable: false),
                    TotalManagersIt = table.Column<int>(type: "int", nullable: false),
                    TotalManagersOperating = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Year = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmRentabilityYearConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmRentabilityYearConfigs_FirmTenantId_Year",
                table: "FirmRentabilityYearConfigs",
                columns: new[] { "FirmTenantId", "Year" },
                unique: true);
        }
    }
}
