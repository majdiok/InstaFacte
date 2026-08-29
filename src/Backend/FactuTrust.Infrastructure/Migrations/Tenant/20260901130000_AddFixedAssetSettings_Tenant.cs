using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Paramètres du module Immobilisations du tenant (plan « Exercices décalés », P1) : crée la
    /// table <c>FixedAssetSettings</c> (singleton par tenant) portant
    /// <c>FiscalYearStartMonth</c> (int, défaut 1 = exercice civil) et
    /// <c>FiscalYearLabelFormat</c> (nvarchar(10), défaut « N/N+1 »). Aucune ligne n'est seedée :
    /// <c>FixedAssetSettingsRepository.GetForTenantAsync</c> retourne le défaut usine (mois 1) tant
    /// qu'aucune ligne n'existe — les tenants existants conservent donc le comportement historique
    /// (exercice civil), sans écriture.
    ///
    /// Migration manuelle (CreateTable ciblée) : le snapshot EF de <c>TenantDbContext</c> comporte
    /// déjà, pour d'autres tables, des colonnes hors-modèle antérieures à cette tâche ; générer
    /// via <c>dotnet ef migrations add</c> aurait entraîné ces changements non liés. Le bloc
    /// d'entité <c>FixedAssetSettings</c> du snapshot est ajouté en conséquence.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260901130000_AddFixedAssetSettings_Tenant")]
    public partial class AddFixedAssetSettings_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FixedAssetSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYearStartMonth = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FiscalYearLabelFormat = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "N/N+1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedAssetSettings", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FixedAssetSettings");
        }
    }
}
