using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Profil d'imputation comptable de la paie, décidable par dossier : crée la table
    /// <c>PayrollAccountingSettings</c> (singleton par tenant) portant <c>AccountProfile</c>
    /// (int, défaut 0 = Legacy), <c>AccountProfileEffectiveDate</c>, <c>InKindOffsetAccount</c>,
    /// <c>DisbursementEntriesEnabled</c>, <c>DetailedSalarySplitEnabled</c> et
    /// <c>EmployeeAuxiliaryEnabled</c> (défaut <c>1</c>, comme la configuration globale).
    ///
    /// <para>
    /// <b>Aucune ligne n'est semée.</b> <c>PayrollAccountingSettingsRepository.GetForTenantAsync</c>
    /// retourne <c>null</c> tant qu'aucune ligne n'existe, et le résolveur retombe alors sur la
    /// configuration globale (<c>AccountingSettings</c> / <c>appsettings</c>). Les dossiers existants
    /// conservent donc leur imputation actuelle à l'octet près, sans écriture ni intervention.
    /// </para>
    ///
    /// Migration manuelle (CreateTable ciblée) : le snapshot EF de <c>TenantDbContext</c> comporte
    /// déjà, pour d'autres tables, des colonnes hors-modèle antérieures à cette tâche ; générer via
    /// <c>dotnet ef migrations add</c> aurait entraîné ces changements non liés. Le bloc d'entité
    /// correspondant du snapshot est ajouté en conséquence.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260901160000_AddPayrollAccountingSettings_Tenant")]
    public partial class AddPayrollAccountingSettings_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollAccountingSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountProfile = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AccountProfileEffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InKindOffsetAccount = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DisbursementEntriesEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DetailedSalarySplitEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EmployeeAuxiliaryEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollAccountingSettings", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollAccountingSettings");
        }
    }
}
