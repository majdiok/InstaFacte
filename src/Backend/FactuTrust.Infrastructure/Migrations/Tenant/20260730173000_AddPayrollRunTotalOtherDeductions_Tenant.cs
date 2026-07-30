using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Ajoute le total agrégé des autres retenues (avances, oppositions) sur le cycle de paie.
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260730173000_AddPayrollRunTotalOtherDeductions_Tenant")]
public partial class AddPayrollRunTotalOtherDeductions_Tenant : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "TotalOtherDeductions",
            table: "PayrollRuns",
            type: "decimal(18,3)",
            precision: 18,
            scale: 3,
            nullable: false,
            defaultValue: 0m);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "TotalOtherDeductions",
            table: "PayrollRuns");
    }
}


