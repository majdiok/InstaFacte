using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Accounting;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

/// <summary>
/// Le compte auxiliaire 425 vient désormais <b>uniquement</b> de la fiche salarié : la dérivation par
/// troncature du matricule a été retirée du chemin d'écriture (elle produisait 10 chiffres, au-delà
/// du plafond de 8, et faisait collisionner deux matricules de même queue). Ce qu'il reste à
/// vérifier ici : la forme héritée est toujours reconnaissable, et le gel refuse un salarié sans
/// compte au lieu d'en inventer un.
/// </summary>
public sealed class PayrollEmployeeAuxiliaryAccountResolverTests
{
    [Theory]
    [InlineData("12", "4250000012")]        // chiffres zéro-paddés sur 7 (suffixe = 10 - 3)
    [InlineData("AB12", "4250000012")]      // lettres ignorées, chiffres conservés
    [InlineData("E123", "4250000123")]      // matricule courant « E » + chiffres
    [InlineData("1234567", "4251234567")]   // exactement la longueur du suffixe
    [InlineData("12345678", "4252345678")]  // trop long → 7 derniers chiffres
    public void ResolveLegacy_DescribesTheHistoricalShape(string employeeNumber, string expected)
    {
        Assert.Equal(expected, PayrollEmployeeAuxiliaryAccountResolver.ResolveLegacy(employeeNumber));
    }

    /// <summary>
    /// La raison d'être du correctif : la forme héritée est hors norme. Un CIN de 8 chiffres
    /// (« 09655554 ») donnait le compte 4259655554 vu en production.
    /// </summary>
    [Fact]
    public void ResolveLegacy_AlwaysExceedsTheDigitCeiling()
    {
        var account = PayrollEmployeeAuxiliaryAccountResolver.ResolveLegacy("09655554");

        Assert.Equal("4259655554", account);
        Assert.Equal(10, account.Length);
        Assert.True(AccountNumberRules.DigitCount(account) > AccountNumberRules.MaxDigits);
        Assert.False(AccountNumberRules.IsWellFormed(account));
    }

    [Fact]
    public void ResolveLegacy_MatriculeWithoutDigits_Throws()
    {
        Assert.Throws<ArgumentException>(() => PayrollEmployeeAuxiliaryAccountResolver.ResolveLegacy("ABC"));
    }

    [Theory]
    [InlineData("ABC", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("A1", true)]
    [InlineData("12", true)]
    public void CanResolveLegacy_DetectsAtLeastOneDigit(string employeeNumber, bool expected)
    {
        Assert.Equal(expected, PayrollEmployeeAuxiliaryAccountResolver.CanResolveLegacy(employeeNumber));
    }

    /// <summary>
    /// Sans compte sur la fiche, le domaine refuse plutôt que de dériver : c'est la couche
    /// Application qui alloue avant d'appeler, et un trou signale une incohérence.
    /// </summary>
    [Fact]
    public void FreezeEmployeeAuxiliaryAccounts_FailsWhenAnEmployeeHasNoAllocatedAccount()
    {
        var run = NewRunWithPayslips(("Jean Dupont", "E123"), ("Karim Saïd", "45"));
        var jean = run.Payslips.Single(p => p.EmployeeNumber == "E123");

        var freeze = run.FreezeEmployeeAuxiliaryAccounts(
            new Dictionary<Guid, string?> { [jean.EmployeeId] = "4250001" });

        Assert.True(freeze.IsFailure);
        Assert.Contains("Karim Saïd", freeze.Error.Description);
    }

    [Fact]
    public void FreezeEmployeeAuxiliaryAccounts_UsesTheAllocatedAccountOnEveryPayslip()
    {
        var run = NewRunWithPayslips(("Jean Dupont", "E123"), ("Karim Saïd", "45"));
        var map = run.Payslips.ToDictionary(
            p => p.EmployeeId,
            p => (string?)(p.EmployeeNumber == "E123" ? "4250001" : "4250002"));

        var freeze = run.FreezeEmployeeAuxiliaryAccounts(map);

        Assert.True(freeze.IsSuccess);
        Assert.Equal(2, freeze.Value.Count);
        Assert.Equal("4250001", run.Payslips.Single(p => p.EmployeeNumber == "E123").EmployeeAuxiliaryAccount);
        Assert.Equal("4250002", run.Payslips.Single(p => p.EmployeeNumber == "45").EmployeeAuxiliaryAccount);
        Assert.All(run.Payslips, p =>
            Assert.True(AccountNumberRules.IsWellFormed(p.EmployeeAuxiliaryAccount)));
    }

    /// <summary>
    /// Deux fiches reprises depuis des bulletins figés par l'ancienne dérivation peuvent porter le
    /// même compte : leurs dettes de salaire se confondraient, le gel doit refuser.
    /// </summary>
    [Fact]
    public void FreezeEmployeeAuxiliaryAccounts_FailsWhenTwoEmployeesShareAnAccount()
    {
        var run = NewRunWithPayslips(("Jean Dupont", "E123"), ("Karim Saïd", "45"));
        var map = run.Payslips.ToDictionary(p => p.EmployeeId, p => (string?)"4250001");

        var freeze = run.FreezeEmployeeAuxiliaryAccounts(map);

        Assert.True(freeze.IsFailure);
        Assert.Contains("4250001", freeze.Error.Description);
    }

    private static PayrollRun NewRunWithPayslips(params (string Name, string Number)[] employees)
    {
        var pars = PayrollParameterDefaults.CreateDefaults(2026).Value;
        var computation = PayrollCalculator.Compute(
            new PayrollComputationInput { BaseSalary = 1000m, Regime = SocialRegime.Rsna, WorkAccidentRate = 0.4m },
            pars);

        var run = PayrollRun.Create(2026, 1, 2026).Value;
        var payslips = employees
            .Select(e => Payslip.FromComputation(
                run.Id, Guid.NewGuid(), e.Name, e.Number, null, 2026, 1, computation, 9.68m, 17.07m))
            .ToList();
        Assert.True(run.SetPayslips(payslips).IsSuccess);
        return run;
    }
}
