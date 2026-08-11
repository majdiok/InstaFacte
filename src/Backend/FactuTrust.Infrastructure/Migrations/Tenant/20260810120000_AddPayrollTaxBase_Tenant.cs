using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Assiette et taux des taxes sur salaires (TFP, FOPROLOS, CSS patronale), figés au calcul.
    ///
    /// <para>
    /// Le formulaire officiel de la déclaration mensuelle doit imprimer la masse salariale en
    /// assiette de la TFP et du FOPROLOS, et choisir la ligne 1 % ou 2 % selon le taux appliqué.
    /// Reconstituer l'un et l'autre depuis les paramètres d'exercice ne tient pas : ces paramètres
    /// restent modifiables, et un changement de taux ferait bouger l'assiette imprimée sur des
    /// déclarations déjà déposées.
    /// </para>
    ///
    /// <para>
    /// Colonnes <b>nullables et sans reprise de données</b> : les bulletins et cycles antérieurs
    /// restent intacts, la déclaration retombant sur une reconstitution pour ceux-là.
    /// <c>ApplyCnssCeilingToPayrollTaxes</c> vaut <c>true</c> par défaut, soit le comportement
    /// historique — aucun montant de paie existant ne bouge.
    /// </para>
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260810120000_AddPayrollTaxBase_Tenant")]
    public partial class AddPayrollTaxBase_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PayrollTaxBase",
                table: "Payslips",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AppliedTfpRate",
                table: "Payslips",
                type: "decimal(8,4)",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPayrollTaxBase",
                table: "PayrollRuns",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AppliedTfpRate",
                table: "PayrollRuns",
                type: "decimal(8,4)",
                precision: 8,
                scale: 4,
                nullable: true);

            // true = plafond CNSS étendu aux taxes sur salaires, soit le comportement d'avant cette
            // migration. Les exercices existants le conservent ; ceux matérialisés depuis les
            // présets légaux passent à false (la TFP et le FOPROLOS n'ont pas de plafond).
            migrationBuilder.AddColumn<bool>(
                name: "ApplyCnssCeilingToPayrollTaxes",
                table: "PayrollYearParameters",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayrollTaxBase",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "AppliedTfpRate",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "TotalPayrollTaxBase",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "AppliedTfpRate",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "ApplyCnssCeilingToPayrollTaxes",
                table: "PayrollYearParameters");
        }
    }
}
