using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Imputation SCE 2026 sur les lignes de bulletin (plan §4 WS-1) :
    /// <list type="bullet">
    /// <item><c>EarningKind</c> — nature du gain (salaire, HS, prime, avantage en nature,
    /// indemnité de rupture…) pilotant le compte de charge cible.</item>
    /// <item><c>AccountSce</c> — compte SCE cible de la ligne (ex. 4386 pour la compensation
    /// en nature), prioritaire sur la table de passage par défaut.</item>
    /// <item><c>SourceEntityId</c> — identifiant de la retenue source (avance / prêt / saisie)
    /// permettant le règlement figé par le bulletin (§4 WS-2).</item>
    /// <item><c>RequestedAmount</c> / <c>CarriedOverAmount</c> — montant demandé et report
    /// portés par la ligne de saisie, figés au calcul.</item>
    /// </list>
    /// Toutes les colonnes sont nullables : <c>null</c> désigne les lignes historiques (profil
    /// Legacy), aucun retraitement n'est appliqué aux bulletins existants.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260831120000_AddPayrollSceEarningMapping_Tenant")]
    public partial class AddPayrollSceEarningMapping_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EarningKind",
                table: "PayslipLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountSce",
                table: "PayslipLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceEntityId",
                table: "PayslipLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RequestedAmount",
                table: "PayslipLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CarriedOverAmount",
                table: "PayslipLines",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarriedOverAmount",
                table: "PayslipLines");

            migrationBuilder.DropColumn(
                name: "RequestedAmount",
                table: "PayslipLines");

            migrationBuilder.DropColumn(
                name: "SourceEntityId",
                table: "PayslipLines");

            migrationBuilder.DropColumn(
                name: "AccountSce",
                table: "PayslipLines");

            migrationBuilder.DropColumn(
                name: "EarningKind",
                table: "PayslipLines");
        }
    }
}
