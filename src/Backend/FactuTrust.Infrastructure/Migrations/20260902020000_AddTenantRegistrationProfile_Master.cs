using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations;

/// <summary>
/// Lot 3 — réponses de profilage saisies à l'inscription : ajoute quatre colonnes NULLABLES à
/// <c>Tenants</c> (SQL idempotent écrit à la main, sans fichier Designer, comme
/// <c>AddTenantSectorCatalogVersion_Master</c> / <c>AddTenantSectorClassification_Master</c>).
///
/// Purement additif : les tenants existants restent à NULL, ce qui signifie « pas de réponse » et
/// non « non ». Aucune de ces colonnes ne gouverne un droit — la résolution des modules
/// (<c>SectorModuleSetCalculator</c>) est inchangée ; elles servent à rejouer la recommandation lors
/// d'un changement de secteur en libre-service et à l'analyse ultérieure.
///
/// <c>HeadcountBand</c> est contraint côté domaine à une liste blanche fermée
/// (<c>Tenant.AllowedHeadcountBands</c>) : aucun texte libre client n'atteint la base.
/// </summary>
[DbContext(typeof(MasterDbContext))]
[Migration("20260902020000_AddTenantRegistrationProfile_Master")]
public partial class AddTenantRegistrationProfile_Master : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Tenants', N'HasPhysicalStock') IS NULL
BEGIN
    ALTER TABLE [Tenants] ADD [HasPhysicalStock] bit NULL;
END

IF COL_LENGTH(N'dbo.Tenants', N'SellsToConsumers') IS NULL
BEGIN
    ALTER TABLE [Tenants] ADD [SellsToConsumers] bit NULL;
END

IF COL_LENGTH(N'dbo.Tenants', N'HeadcountBand') IS NULL
BEGIN
    ALTER TABLE [Tenants] ADD [HeadcountBand] nvarchar(10) NULL;
END

IF COL_LENGTH(N'dbo.Tenants', N'AccountingDelegatedToFirm') IS NULL
BEGIN
    ALTER TABLE [Tenants] ADD [AccountingDelegatedToFirm] bit NULL;
END
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'dbo.Tenants', N'AccountingDelegatedToFirm') IS NOT NULL
BEGIN
    ALTER TABLE [Tenants] DROP COLUMN [AccountingDelegatedToFirm];
END

IF COL_LENGTH(N'dbo.Tenants', N'HeadcountBand') IS NOT NULL
BEGIN
    ALTER TABLE [Tenants] DROP COLUMN [HeadcountBand];
END

IF COL_LENGTH(N'dbo.Tenants', N'SellsToConsumers') IS NOT NULL
BEGIN
    ALTER TABLE [Tenants] DROP COLUMN [SellsToConsumers];
END

IF COL_LENGTH(N'dbo.Tenants', N'HasPhysicalStock') IS NOT NULL
BEGIN
    ALTER TABLE [Tenants] DROP COLUMN [HasPhysicalStock];
END
""");
    }
}
