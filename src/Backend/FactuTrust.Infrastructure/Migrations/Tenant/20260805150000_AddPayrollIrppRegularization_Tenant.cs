using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Régularisation IRPP/CSS annuelle : table des régularisations saisies ou générées par
    /// salarié et par mois, colonnes de portage sur le bulletin et le cycle, et drapeau
    /// d'activation par exercice.
    ///
    /// Toutes les colonnes ajoutées portent un défaut neutre (0 / false) : les bulletins et
    /// cycles déjà calculés conservent exactement leur montant et leur comportement.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260805150000_AddPayrollIrppRegularization_Tenant")]
    public partial class AddPayrollIrppRegularization_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollIrppRegularizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    MonthsCounted = table.Column<int>(type: "int", nullable: false),
                    CumulNetTaxable = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CumulIrppWithheld = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CumulCssWithheld = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    IrppDue = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CssDue = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ComputedIrppDelta = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ComputedCssDelta = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OverrideIrppDelta = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    OverrideCssDelta = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DetailJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollIrppRegularizations", x => x.Id);
                });

            // Une seule régularisation par salarié et par mois : la régénération remplace.
            migrationBuilder.CreateIndex(
                name: "IX_PayrollIrppRegularizations_EmployeeId_Year_Month",
                table: "PayrollIrppRegularizations",
                columns: new[] { "EmployeeId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollIrppRegularizations_Year_Month",
                table: "PayrollIrppRegularizations",
                columns: new[] { "Year", "Month" });

            // Portage sur le bulletin (montants signés : positif = rappel, négatif = restitution).
            migrationBuilder.AddColumn<decimal>(
                name: "IrppRegularization",
                table: "Payslips",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CssRegularization",
                table: "Payslips",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularizationDeferred",
                table: "Payslips",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            // Totaux du cycle, tenus à part de TotalIrpp qui garde le sens d'IRPP mensuel.
            migrationBuilder.AddColumn<decimal>(
                name: "TotalIrppRegularization",
                table: "PayrollRuns",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCssRegularization",
                table: "PayrollRuns",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            // Option d'exercice : désactivée par défaut pour les tenants existants.
            migrationBuilder.AddColumn<bool>(
                name: "EnableIrppRegularization",
                table: "PayrollYearParameters",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableIrppRegularization",
                table: "PayrollYearParameters");

            migrationBuilder.DropColumn(
                name: "TotalCssRegularization",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "TotalIrppRegularization",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "RegularizationDeferred",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "CssRegularization",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "IrppRegularization",
                table: "Payslips");

            migrationBuilder.DropTable(
                name: "PayrollIrppRegularizations");
        }
    }
}
