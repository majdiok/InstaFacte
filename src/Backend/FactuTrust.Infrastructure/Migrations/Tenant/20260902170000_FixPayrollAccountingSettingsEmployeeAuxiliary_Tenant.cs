using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Rattrape <c>PayrollAccountingSettings.EmployeeAuxiliaryEnabled</c> sur les bases où la table a
/// été créée sans cette colonne.
///
/// <para>
/// <b>Origine.</b> <c>20260901160000_AddPayrollAccountingSettings_Tenant</c> a été appliquée sur une
/// partie du parc dans un état antérieur à l'ajout de la colonne, puis complétée sur place. EF Core
/// ne rejoue jamais une migration inscrite dans <c>__EFMigrationsHistory</c> : ces bases portent donc
/// la table sans la colonne, alors que <c>TenantDbContext</c> la projette dans chaque <c>SELECT</c>.
/// Toute lecture du réglage échoue en <c>SqlException</c> 207, ce qui bloque la validation d'un cycle
/// de paie, l'écran Paramètres paie et le journal de paie.
/// </para>
///
/// <para>
/// <b>Neutralité.</b> Le défaut <c>1</c> reprend à l'identique celui de la migration d'origine, du
/// domaine (<c>PayrollAccountingSettings.EmployeeAuxiliaryEnabled</c>), du mapping
/// (<c>HasDefaultValue(true)</c>) et de la configuration globale
/// (<c>Accounting:PayrollEmployeeAuxiliaryEnabled</c>) : une base réparée devient indiscernable d'une
/// base créée normalement. Aucune reprise de données — la table est un singleton par tenant, vide
/// tant que le dossier n'a pas été paramétré, et son absence de ligne vaut toujours « repli sur la
/// configuration globale ».
/// </para>
///
/// <para>
/// <b>Pourquoi une migration et pas seulement un script.</b> Le provisionnement par défaut est
/// <c>TemplateClone</c>, et <c>TenantTemplateFreshness.CanRestoreWithoutRebuild</c> juge la base
/// modèle réutilisable dès qu'aucune migration n'est en attente. Un modèle dérivé serait donc cloné
/// tel quel vers chaque nouveau dossier. Publier une migration le rend obsolète et force sa
/// reconstruction.
/// </para>
///
/// Migration manuelle, additive et gardée — snapshot EF volontairement non modifié (désynchronisé
/// depuis 2026-08-01, cf. docs/backend-tenant-migrations.md ; il déclare déjà la colonne).
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260902170000_FixPayrollAccountingSettingsEmployeeAuxiliary_Tenant")]
public partial class FixPayrollAccountingSettingsEmployeeAuxiliary_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[PayrollAccountingSettings]', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.PayrollAccountingSettings', N'EmployeeAuxiliaryEnabled') IS NULL
BEGIN
    ALTER TABLE [dbo].[PayrollAccountingSettings]
        ADD [EmployeeAuxiliaryEnabled] bit NOT NULL
        CONSTRAINT [DF_PayrollAccountingSettings_EmployeeAuxiliaryEnabled] DEFAULT CONVERT(bit, 1);
END
""");
    }

    /// <summary>
    /// Volontairement sans effet. La colonne appartient à
    /// <c>20260901160000_AddPayrollAccountingSettings_Tenant</c> — c'est cette migration-là qui la
    /// supprime en <c>Down()</c>, avec la table. La retirer ici casserait toute base créée
    /// normalement, qui la tient de sa propre création de table et non de ce rattrapage.
    /// </summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
