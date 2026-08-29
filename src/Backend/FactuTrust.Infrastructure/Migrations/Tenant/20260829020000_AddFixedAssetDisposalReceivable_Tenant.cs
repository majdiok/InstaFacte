using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Créance sur cession d'immobilisations (compte 452) et cession gratuite / mise au rebut
    /// (T4, A6) : ajoute <c>FixedAssets.DisposalReceivableAccount</c> (nvarchar(20), NULL) pour
    /// le règlement à terme d'une cession, à côté du <c>DisposalTreasuryAccount</c> existant
    /// (règlement comptant). Un produit de cession nul (mise au rebut) n'exige aucun compte.
    ///
    /// Migration manuelle (AddColumn ciblée) : le snapshot EF de <c>TenantDbContext</c> comporte
    /// déjà, pour d'autres tables, des colonnes hors-modèle antérieures à cette tâche ; générer
    /// via <c>dotnet ef migrations add</c> aurait entraîné ces changements non liés. Le bloc
    /// d'entité <c>FixedAsset</c> du snapshot est mis à jour en conséquence (cf. T2).
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260829020000_AddFixedAssetDisposalReceivable_Tenant")]
    public partial class AddFixedAssetDisposalReceivable_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisposalReceivableAccount",
                table: "FixedAssets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisposalReceivableAccount",
                table: "FixedAssets");
        }
    }
}
