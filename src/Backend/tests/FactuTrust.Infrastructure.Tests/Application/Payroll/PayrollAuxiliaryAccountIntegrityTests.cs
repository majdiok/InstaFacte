using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

/// <summary>
/// Intégrité des comptes auxiliaires 425xxxxxxx : le compte n'emprunte que les 7 derniers chiffres
/// du matricule, deux salariés peuvent donc y aboutir ensemble. La validation d'un cycle doit le
/// refuser plutôt que de confondre leurs dettes de salaire.
/// </summary>
public sealed class PayrollAuxiliaryAccountIntegrityTests
{
    private static PayrollRun BuildRun(params (string Number, string Name)[] employees)
    {
        var pars = PayrollParameterDefaults.CreateDefaults(2026).Value;
        var run = PayrollRun.Create(2026, 8, 2026).Value;
        var input = new PayrollComputationInput { BaseSalary = 2000m, Regime = SocialRegime.Rsna };
        var computation = PayrollCalculator.Compute(input, pars);
        var (empRate, employerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, pars);

        var payslips = employees
            .Select(e => Payslip.FromComputation(
                run.Id, Guid.NewGuid(), e.Name, e.Number, null, 2026, 8, computation, empRate, employerRate))
            .ToList();

        run.SetPayslips(payslips);
        return run;
    }

    [Fact]
    public void Freeze_AssignsDistinctAccounts_WhenSuffixesDiffer()
    {
        var run = BuildRun(("EMP-001", "Alice"), ("EMP-002", "Bob"));

        var result = run.FreezeEmployeeAuxiliaryAccounts();

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var accounts = run.Payslips.Select(p => p.EmployeeAuxiliaryAccount).ToList();
        Assert.Equal(2, accounts.Distinct(StringComparer.Ordinal).Count());
        Assert.All(accounts, a => Assert.StartsWith("425", a));
    }

    [Fact]
    public void Freeze_Rejects_WhenTwoNumbersCollapseToTheSameAccount()
    {
        // Le résolveur ne garde que les chiffres, tronqués aux 7 derniers et zéro-paddés :
        // « 1 » et « 0000001 » produisent tous deux 4250000001.
        var run = BuildRun(("1", "Alice"), ("0000001", "Bob"));

        var result = run.FreezeEmployeeAuxiliaryAccounts();

        Assert.True(result.IsFailure);
        Assert.Contains("même compte", result.Error.Description);
        Assert.Contains("Alice", result.Error.Description);
        Assert.Contains("Bob", result.Error.Description);
    }

    [Fact]
    public void Freeze_Rejects_WhenLongNumbersShareTheirLastSevenDigits()
    {
        var run = BuildRun(("991234567", "Alice"), ("881234567", "Bob"));

        var result = run.FreezeEmployeeAuxiliaryAccounts();

        Assert.True(result.IsFailure);
        Assert.Contains("4251234567", result.Error.Description);
    }

    [Fact]
    public void Freeze_Rejects_WhenEmployeeNumberHasNoDigit()
    {
        var run = BuildRun(("ABC", "Alice"));

        var result = run.FreezeEmployeeAuxiliaryAccounts();

        Assert.True(result.IsFailure);
        Assert.Contains("aucun chiffre", result.Error.Description);
    }

    [Fact]
    public void Freeze_IsIdempotent_AndKeepsAlreadyFrozenAccounts()
    {
        var run = BuildRun(("EMP-001", "Alice"), ("EMP-002", "Bob"));

        var first = run.FreezeEmployeeAuxiliaryAccounts();
        Assert.True(first.IsSuccess);
        Assert.Equal(2, first.Value.Count);
        var frozen = run.Payslips.Select(p => p.EmployeeAuxiliaryAccount).ToList();

        // Second passage (cycle recalculé) : rien de nouveau à figer, comptes inchangés.
        var second = run.FreezeEmployeeAuxiliaryAccounts();
        Assert.True(second.IsSuccess);
        Assert.Empty(second.Value);
        Assert.Equal(frozen, run.Payslips.Select(p => p.EmployeeAuxiliaryAccount));
    }

    [Theory]
    [InlineData("1", "4250000001")]
    [InlineData("EMP-0042", "4250000042")]
    [InlineData("123456789012", "4256789012")]   // tronqué aux 7 derniers chiffres
    public void Resolver_ProducesNumericAccountsOfTenCharacters(string employeeNumber, string expected)
    {
        var account = PayrollEmployeeAuxiliaryAccountResolver.Resolve(employeeNumber);

        Assert.Equal(expected, account);
        Assert.Equal(10, account.Length);
        Assert.All(account, c => Assert.True(char.IsDigit(c)));
    }
}
