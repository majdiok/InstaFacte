using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Compte auxiliaire 425 explicite sur la fiche salarié : ajoute
    /// <c>Employees.AuxiliaryAccountNumber</c> (nvarchar(32), nullable).
    ///
    /// <para>
    /// <b>Aucune reprise de données.</b> La colonne reste <c>NULL</c> pour tous les salariés
    /// existants, et le repli sur la dérivation historique
    /// (<c>PayrollEmployeeAuxiliaryAccountResolver</c>, 425 + 7 derniers chiffres du matricule)
    /// s'applique alors comme avant : aucun compte ne change de numéro, aucun solde ne se déplace.
    /// Seuls les salariés créés à partir de cette version reçoivent un compte alloué séquentiellement.
    /// </para>
    ///
    /// Migration manuelle (AddColumn ciblé) : le snapshot EF de <c>TenantDbContext</c> comporte déjà,
    /// pour d'autres tables, des colonnes hors-modèle antérieures à cette tâche ; générer via
    /// <c>dotnet ef migrations add</c> aurait entraîné ces changements non liés.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260901180000_AddEmployeeAuxiliaryAccount_Tenant")]
    public partial class AddEmployeeAuxiliaryAccount_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuxiliaryAccountNumber",
                table: "Employees",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuxiliaryAccountNumber",
                table: "Employees");
        }
    }
}
