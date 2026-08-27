using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Total du net imposable salarial figé sur le cycle (<c>TotalNetTaxable</c>), assiette des
    /// articles 1 (IRPP) et 3 (CSS) du formulaire officiel de déclaration mensuelle.
    ///
    /// Les cycles déjà calculés / validés / clôturés ne passent plus par
    /// <c>RecomputeTotals</c> : la colonne est reprise depuis <c>Payslips.MonthlyNetTaxable</c>.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260827120000_AddPayrollRunTotalNetTaxable_Tenant")]
    public partial class AddPayrollRunTotalNetTaxable_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TotalNetTaxable",
                table: "PayrollRuns",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE r
                SET r.[TotalNetTaxable] = ISNULL((
                    SELECT SUM(p.[MonthlyNetTaxable])
                    FROM [Payslips] p
                    WHERE p.[PayrollRunId] = r.[Id]
                ), 0)
                FROM [PayrollRuns] r;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalNetTaxable",
                table: "PayrollRuns");
        }
    }
}
