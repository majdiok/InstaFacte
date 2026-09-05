using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.Accounting;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Accounting;

/// <summary>
/// Durcissement CWE-89 et exhaustivité de l'inventaire des colonnes de la renumérotation.
/// </summary>
/// <remarks>
/// Les noms de table et de colonne sont interpolés dans du SQL brut (identifiants entre crochets,
/// non paramétrables) : seules les paires de l'allow-list sont lisibles, contrôle effectué avant
/// tout accès à la base. Cette liste est <b>distincte</b> de celle de
/// <see cref="Nct01ChartMigrationService"/> : élargir l'une ne doit jamais élargir la surface SQL
/// brute de l'autre.
/// </remarks>
public sealed class ChartAccountDigitCompactionAllowListTests
{
    /// <summary>
    /// Les six paires que l'inventaire du remap NCT 01 ne couvrait pas : elles appartiennent à des
    /// entités plus récentes que lui. Sans <c>Employees.AuxiliaryAccountNumber</c>, la fiche
    /// salarié pointerait un compte disparu après renumérotation.
    /// </summary>
    [Theory]
    [InlineData("Employees", "AuxiliaryAccountNumber")]
    [InlineData("PayslipLines", "AccountSce")]
    [InlineData("SocialFundSchemes", "EmployeeAccountSce")]
    [InlineData("SocialFundSchemes", "EmployerAccountSce")]
    [InlineData("PayrollAccountingSettings", "InKindOffsetAccount")]
    [InlineData("FixedAssets", "DisposalReceivableAccount")]
    public void Inventory_CoversTheColumnsMissingFromTheNct01Remap(string table, string column)
    {
        Assert.Contains((table, column), ChartAccountDigitCompactionService.ReferencingColumns);
        Assert.DoesNotContain((table, column), Nct01ChartMigrationService.DistinctValuesAllowList);
    }

    /// <summary>Les colonnes qui portent la dette de salaire et son règlement.</summary>
    [Theory]
    [InlineData("JournalEntryLines", "AccountNumber")]
    [InlineData("LetteringGroups", "AccountNumber")]
    [InlineData("Payslips", "EmployeeAuxiliaryAccount")]
    [InlineData("PayrollPaymentLines", "EmployeeAuxiliaryAccount")]
    public void Inventory_CoversTheCoreAccountingColumns(string table, string column)
    {
        Assert.Contains((table, column), ChartAccountDigitCompactionService.ReferencingColumns);
    }

    /// <summary>
    /// <c>BudgetPosts.AccountPrefixes</c> est délibérément <b>hors</b> inventaire :
    /// <see cref="BudgetPost.ParsePrefixes"/> plafonne déjà chaque préfixe à 8 chiffres, la colonne
    /// ne peut donc pas contenir un numéro que la renumérotation déplace. L'inclure ajouterait une
    /// surface SQL brute sur une colonne à liste sans rien corriger.
    /// </summary>
    [Fact]
    public void Inventory_ExcludesBudgetPrefixes_WhichCannotHoldAnOverlongNumber()
    {
        Assert.DoesNotContain(("BudgetPosts", "AccountPrefixes"), ChartAccountDigitCompactionService.ReferencingColumns);

        var tooLong = BudgetPost.ParsePrefixes("123456789");
        Assert.True(tooLong.IsFailure);

        var compliant = BudgetPost.ParsePrefixes("61;62");
        Assert.True(compliant.IsSuccess);
    }

    /// <summary>
    /// La renumérotation lit les tables du domaine ; le journal des correspondances a sa propre
    /// allow-list, plus étroite. Aucune des deux ne doit ouvrir l'autre.
    /// </summary>
    [Fact]
    public void Inventory_DoesNotExposeTheCompactionLedgerItself()
    {
        Assert.DoesNotContain(
            ("ChartOfAccountCompactionLogs", "FromAccountNumber"),
            ChartAccountDigitCompactionService.ReferencingColumns);
        Assert.DoesNotContain(
            ("ChartOfAccountCompactionLogs", "ToAccountNumber"),
            ChartAccountDigitCompactionService.ReferencingColumns);
    }

    [Theory]
    [InlineData("Users", "PasswordHash")]
    [InlineData("JournalEntryLines", "TenantId")]
    [InlineData("JournalEntryLines]; DROP TABLE Users; --", "AccountNumber")]
    public void Inventory_RejectsAnythingElse(string table, string column)
    {
        Assert.DoesNotContain((table, column), ChartAccountDigitCompactionService.ReferencingColumns);
    }
}
