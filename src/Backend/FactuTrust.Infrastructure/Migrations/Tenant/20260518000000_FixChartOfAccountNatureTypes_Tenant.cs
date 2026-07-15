using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Corrige la nature comptable de comptes de classe 4 mal seedés dans
/// <c>AddAccountingModule_Tenant</c>. Les comptes 4341 « Retenue à la source » et
/// 43667 « Crédit de TVA à reporter » sont des créances sur l'État — nature Débit (Actif) —
/// et non des dettes. Le Bilan classant désormais la classe 4 par la nature du compte,
/// sans cette correction ces comptes apparaîtraient à tort au Passif sur les tenants existants.
///
/// Migration de données de référence uniquement : aucune écriture comptable n'est modifiée.
/// NatureType : 0 = Débit (Actif), 1 = Crédit (Passif).
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260518000000_FixChartOfAccountNatureTypes_Tenant")]
public partial class FixChartOfAccountNatureTypes_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE [ChartOfAccounts] SET [NatureType] = 0 " +
            "WHERE [AccountNumber] IN ('4341', '43667') AND [NatureType] <> 0;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE [ChartOfAccounts] SET [NatureType] = 1 " +
            "WHERE [AccountNumber] IN ('4341', '43667') AND [NatureType] <> 1;");
    }
}
