using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Correctifs fiscaux de la détermination du résultat fiscal :
/// - régime de minimum d'impôt sur la déclaration (droit commun / réduit / exonéré) ;
/// - plancher du minimum d'impôt au régime réduit ;
/// - arrondi de l'assiette au dinar ;
/// - marqueur de paramètres saisis par le comptable (jamais écrasés par les défauts).
/// Migration strictement ADDITIVE : aucune colonne existante n'est modifiée ni supprimée.
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260721120000_AddFiscalMinimumRegimeAndParams_Tenant")]
public partial class AddFiscalMinimumRegimeAndParams_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "MinTaxFloorReducedTnd",
            table: "IncomeTaxYearParameters",
            type: "decimal(18,3)",
            precision: 18,
            scale: 3,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<bool>(
            name: "RoundTaxableToDinar",
            table: "IncomeTaxYearParameters",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsUserModified",
            table: "IncomeTaxYearParameters",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "MinimumTaxRegime",
            table: "FiscalResultDeclarations",
            type: "int",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "MinimumTaxRegime", table: "FiscalResultDeclarations");
        migrationBuilder.DropColumn(name: "IsUserModified", table: "IncomeTaxYearParameters");
        migrationBuilder.DropColumn(name: "RoundTaxableToDinar", table: "IncomeTaxYearParameters");
        migrationBuilder.DropColumn(name: "MinTaxFloorReducedTnd", table: "IncomeTaxYearParameters");
    }
}
