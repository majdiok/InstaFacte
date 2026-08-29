using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// TVA à l'acquisition capitalisable (véhicules de tourisme, art. 9 code TVA) : ajoute
    /// <c>FixedAssets.VatCapitalized</c>, calculée par <c>FixedAssetVatRules.IsVatCapitalized</c>
    /// selon la catégorie NCT et le compte d'immobilisation effectif de la ligne.
    ///
    /// Migration manuelle (AddColumn ciblée) : le snapshot EF de <c>TenantDbContext</c> comporte
    /// déjà, pour d'autres tables, des colonnes hors-modèle antérieures à cette tâche ; générer
    /// via <c>dotnet ef migrations add</c> aurait entraîné ces changements non liés. Le bloc
    /// d'entité <c>FixedAsset</c> du snapshot est mis à jour en conséquence.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260829010000_AddFixedAssetVatCapitalized_Tenant")]
    public partial class AddFixedAssetVatCapitalized_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "VatCapitalized",
                table: "FixedAssets",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VatCapitalized",
                table: "FixedAssets");
        }
    }
}
