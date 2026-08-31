using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

/// <summary>
/// R-15 : le compte auxiliaire SCE 425 doit être strictement numérique. L'ancien repli
/// alphanumérique (ex. 42500AB12) est rejeté. Le compte est figé à la validation du cycle, et non
/// plus paresseusement au paiement/OD — un changement de matricule entre validation et paiement n'a
/// plus d'effet.
/// </summary>
public sealed class PayrollEmployeeAuxiliaryAccountResolverTests
{
    [Theory]
    [InlineData("12", "4250000012")]        // digits zero-padded sur 7 (suffixe = 10 - 3)
    [InlineData("AB12", "4250000012")]      // lettres ignorées, chiffres conservés (R-15)
    [InlineData("E123", "4250000123")]      // matricule courant « E » + chiffres
    [InlineData("1234567", "4251234567")]   // exactement la longueur du suffixe
    [InlineData("12345678", "4252345678")]  // trop long → 7 derniers chiffres
    public void Resolve_ProducesStrictlyNumericAccount(string employeeNumber, string expected)
    {
        Assert.Equal(expected, PayrollEmployeeAuxiliaryAccountResolver.Resolve(employeeNumber));
    }

    [Fact]
    public void Resolve_MatriculeWithoutDigits_Throws()
    {
        // R-15 : aucun chiffre → impossible de produire un compte SCE numérique.
        Assert.Throws<ArgumentException>(() => PayrollEmployeeAuxiliaryAccountResolver.Resolve("ABC"));
    }

    [Theory]
    [InlineData("ABC", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("A1", true)]
    [InlineData("12", true)]
    public void CanResolve_DetectsAtLeastOneDigit(string employeeNumber, bool expected)
    {
        Assert.Equal(expected, PayrollEmployeeAuxiliaryAccountResolver.CanResolve(employeeNumber));
    }

    [Fact]
    public void FreezeEmployeeAuxiliaryAccounts_FailsWhenAMatriculeHasNoDigits()
    {
        var run = NewRunWithPayslips(("Marie Lettre", "ABC"), ("Jean Dupont", "E123"));

        var freeze = run.FreezeEmployeeAuxiliaryAccounts();

        // R-15 : la validation ne doit pas produire un compte invalide ; échec nominatif.
        Assert.True(freeze.IsFailure);
    }

    [Fact]
    public void FreezeEmployeeAuxiliaryAccounts_SetsNumericAccountOnEveryPayslip()
    {
        var run = NewRunWithPayslips(("Jean Dupont", "E123"), ("Karim Saïd", "45"));

        var freeze = run.FreezeEmployeeAuxiliaryAccounts();

        Assert.True(freeze.IsSuccess);
        Assert.Equal(2, freeze.Value.Count);
        Assert.All(run.Payslips, p => Assert.False(string.IsNullOrWhiteSpace(p.EmployeeAuxiliaryAccount)));
        Assert.Equal("4250000123", run.Payslips.Single(p => p.EmployeeNumber == "E123").EmployeeAuxiliaryAccount);
        Assert.Equal("4250000045", run.Payslips.Single(p => p.EmployeeNumber == "45").EmployeeAuxiliaryAccount);
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
