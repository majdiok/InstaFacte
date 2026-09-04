using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Accounting;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

/// <summary>
/// Intégrité des comptes auxiliaires 425 au gel du cycle. Le compte vient exclusivement de la fiche
/// salarié : le domaine n'en dérive plus aucun du matricule. Deux salariés ne peuvent pas partager
/// un compte — leurs dettes de salaire se confondraient et le lettrage du règlement deviendrait
/// arbitraire.
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

    /// <summary>Alloue un compte séquentiel à chaque bulletin, comme le fait la couche Application.</summary>
    private static Dictionary<Guid, string?> AllocateAll(PayrollRun run)
    {
        var map = new Dictionary<Guid, string?>();
        var next = 1;
        foreach (var payslip in run.Payslips)
            map[payslip.EmployeeId] = "425" + next++.ToString("0000");
        return map;
    }

    [Fact]
    public void Freeze_AssignsTheAllocatedAccounts()
    {
        var run = BuildRun(("EMP-001", "Alice"), ("EMP-002", "Bob"));

        var result = run.FreezeEmployeeAuxiliaryAccounts(AllocateAll(run));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var accounts = run.Payslips.Select(p => p.EmployeeAuxiliaryAccount).ToList();
        Assert.Equal(2, accounts.Distinct(StringComparer.Ordinal).Count());
        Assert.All(accounts, a => Assert.StartsWith("425", a));
    }

    /// <summary>
    /// Deux matricules dont les 7 derniers chiffres coïncidaient ne collisionnent plus : l'ancienne
    /// dérivation les aurait tous deux envoyés sur 4251234567, l'allocation séquentielle non.
    /// </summary>
    [Fact]
    public void Freeze_Accepts_MatriculesThatTheOldDerivationWouldHaveCollided()
    {
        var run = BuildRun(("991234567", "Alice"), ("881234567", "Bob"));

        Assert.Equal(
            PayrollEmployeeAuxiliaryAccountResolver.ResolveLegacy("991234567"),
            PayrollEmployeeAuxiliaryAccountResolver.ResolveLegacy("881234567"));

        var result = run.FreezeEmployeeAuxiliaryAccounts(AllocateAll(run));

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(
            2,
            run.Payslips.Select(p => p.EmployeeAuxiliaryAccount).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// Une collision reste possible via des fiches reprises depuis des bulletins figés par l'ancienne
    /// dérivation : le gel doit encore la refuser.
    /// </summary>
    [Fact]
    public void Freeze_Rejects_WhenTwoEmployeesCarryTheSameAccount()
    {
        var run = BuildRun(("1", "Alice"), ("0000001", "Bob"));
        var map = run.Payslips.ToDictionary(p => p.EmployeeId, p => (string?)"4250000001");

        var result = run.FreezeEmployeeAuxiliaryAccounts(map);

        Assert.True(result.IsFailure);
        Assert.Contains("même compte", result.Error.Description);
        Assert.Contains("Alice", result.Error.Description);
        Assert.Contains("Bob", result.Error.Description);
    }

    /// <summary>
    /// Un matricule sans chiffre n'est plus un cas d'échec en soi — le compte ne s'en déduit plus.
    /// C'est l'absence de compte alloué qui bloque, avec un message qui nomme le salarié.
    /// </summary>
    [Fact]
    public void Freeze_Rejects_WhenNoAccountWasAllocated()
    {
        var run = BuildRun(("ABC", "Alice"));

        var result = run.FreezeEmployeeAuxiliaryAccounts(new Dictionary<Guid, string?>());

        Assert.True(result.IsFailure);
        Assert.Contains("Alice", result.Error.Description);
        Assert.Contains("compte auxiliaire", result.Error.Description);
    }

    [Fact]
    public void Freeze_IsIdempotent_AndKeepsAlreadyFrozenAccounts()
    {
        var run = BuildRun(("EMP-001", "Alice"), ("EMP-002", "Bob"));
        var map = AllocateAll(run);

        var first = run.FreezeEmployeeAuxiliaryAccounts(map);
        Assert.True(first.IsSuccess);
        Assert.Equal(2, first.Value.Count);
        var frozen = run.Payslips.Select(p => p.EmployeeAuxiliaryAccount).ToList();

        // Second passage (cycle recalculé) : rien de nouveau à figer, comptes inchangés.
        var second = run.FreezeEmployeeAuxiliaryAccounts(map);
        Assert.True(second.IsSuccess);
        Assert.Empty(second.Value);
        Assert.Equal(frozen, run.Payslips.Select(p => p.EmployeeAuxiliaryAccount));
    }

    /// <summary>
    /// La forme héritée reste reconnaissable — c'est ce qui permet à la renumérotation de rapprocher
    /// un compte 425xxxxxxx de son matricule — mais elle est hors norme, d'où le correctif.
    /// </summary>
    [Theory]
    [InlineData("1", "4250000001")]
    [InlineData("EMP-0042", "4250000042")]
    [InlineData("123456789012", "4256789012")]   // tronqué aux 7 derniers chiffres
    public void ResolveLegacy_ProducesTenDigitAccountsThatTheRulesReject(string employeeNumber, string expected)
    {
        var account = PayrollEmployeeAuxiliaryAccountResolver.ResolveLegacy(employeeNumber);

        Assert.Equal(expected, account);
        Assert.Equal(10, account.Length);
        Assert.All(account, c => Assert.True(char.IsDigit(c)));
        Assert.False(AccountNumberRules.IsWellFormed(account));
    }
}
