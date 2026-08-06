using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Correctif : suppression de la colonne Version sur trois tables filles dont l'entité C#
    /// hérite d'Entity (et non de AggregateRoot). Ces colonnes NOT NULL sans valeur par défaut
    /// bloquaient les INSERT car EF Core n'émet aucune valeur (la propriété n'existe pas).
    ///
    /// Tables affectées :
    ///  - PayrollGarnishmentBrackets          (entité PayrollGarnishmentBracket : Entity)
    ///  - EmployeeLoanInstallments            (entité EmployeeLoanInstallment : Entity)
    ///  - EmployeeGarnishmentInstallments     (entité EmployeeGarnishmentInstallment : Entity)
    ///
    /// Sans-régression : aucun code lit ou écrit ces colonnes (elles sont invisibles pour EF Core),
    /// et aucune donnée métier n'y est stockée puisque l'INSERT échouait avant d'y écrire.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20260808400000_DropVersionOnPayrollChildEntities_Tenant")]
    public partial class DropVersionOnPayrollChildEntities_Tenant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Version", table: "PayrollGarnishmentBrackets");
            migrationBuilder.DropColumn(name: "Version", table: "EmployeeLoanInstallments");
            migrationBuilder.DropColumn(name: "Version", table: "EmployeeGarnishmentInstallments");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "PayrollGarnishmentBrackets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "EmployeeLoanInstallments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "EmployeeGarnishmentInstallments",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
