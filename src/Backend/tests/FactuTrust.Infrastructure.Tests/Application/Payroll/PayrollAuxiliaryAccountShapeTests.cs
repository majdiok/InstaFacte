using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

/// <summary>
/// Forme des comptes auxiliaires salariés : libellé auto-généré, compte alloué sur la fiche, et
/// repli sur la dérivation historique pour les salariés antérieurs.
/// </summary>
public sealed class PayrollAuxiliaryAccountShapeTests
{
    // ── Libellé d'un sous-compte auto-créé ──────────────────────────────────────────────────

    [Fact]
    public void AutoLabel_DoesNotStackNumericSuffixes()
    {
        // Le défaut d'origine : chaque niveau d'auto-création ajoutait son numéro au libellé du
        // parent, produisant « Personnel et comptes rattachés — 421 — 4218744456 » — un libellé qui
        // cite un compte différent de celui qu'il désigne.
        var label = AccountingService.BuildAutoSubAccountLabel(
            "Personnel et comptes rattachés — 421", "4218744456");

        Assert.Equal("Personnel et comptes rattachés — 4218744456", label);
    }

    [Fact]
    public void AutoLabel_StripsSeveralStackedSuffixes()
    {
        var label = AccountingService.BuildAutoSubAccountLabel(
            "Personnel et comptes rattachés — 421 — 4218744456", "4258744456");

        Assert.Equal("Personnel et comptes rattachés — 4258744456", label);
    }

    [Fact]
    public void AutoLabel_KeepsNonNumericTails()
    {
        // Un libellé métier contenant un tiret cadratin ne doit pas être amputé.
        var label = AccountingService.BuildAutoSubAccountLabel("Banque — compte courant", "5321004");

        Assert.Equal("Banque — compte courant — 5321004", label);
    }

    [Fact]
    public void AutoLabel_FallsBackToAccountNumber_WhenParentLabelIsOnlyANumber()
    {
        Assert.Equal("4250001", AccountingService.BuildAutoSubAccountLabel("425", "4250001"));
    }

    // ── Compte auxiliaire porté par la fiche salarié ────────────────────────────────────────

    [Theory]
    [InlineData("4250001")]
    [InlineData("4258744456")]
    public void Employee_AcceptsSubAccountsOfTheCollective(string account)
    {
        var employee = BuildEmployee();

        var result = employee.SetAuxiliaryAccountNumber(account);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(account, employee.AuxiliaryAccountNumber);
    }

    [Theory]
    [InlineData("425")]        // le collectif lui-même n'est pas un auxiliaire
    [InlineData("4210001")]    // hors de la branche « rémunérations dues »
    [InlineData("425.1")]      // un compte de salarié doit rester strictement numérique
    [InlineData("EMP-01")]
    public void Employee_RejectsAccountsOutsideTheCollective(string account)
    {
        var result = BuildEmployee().SetAuxiliaryAccountNumber(account);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Employee_AcceptsClearingTheAccount()
    {
        var employee = BuildEmployee();
        employee.SetAuxiliaryAccountNumber("4250001");

        Assert.True(employee.SetAuxiliaryAccountNumber(null).IsSuccess);
        Assert.Null(employee.AuxiliaryAccountNumber);
    }

    // ── Figeage à la validation ─────────────────────────────────────────────────────────────

    [Fact]
    public void Freeze_PrefersTheAccountAssignedOnTheEmployeeFile()
    {
        var run = BuildRun(("EMP-8744456", "Karim Soumi"));
        var employeeId = run.Payslips.First().EmployeeId;

        var result = run.FreezeEmployeeAuxiliaryAccounts(
            new Dictionary<Guid, string?> { [employeeId] = "4250001" });

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("4250001", run.Payslips.First().EmployeeAuxiliaryAccount);
    }

    [Fact]
    public void Freeze_WithoutAssignment_KeepsTheHistoricalDerivation()
    {
        // Test de non-régression central : un dossier dont aucune fiche ne porte de compte alloué
        // doit produire exactement les mêmes comptes qu'avant l'introduction du champ.
        var run = BuildRun(("EMP-8744456", "Karim Soumi"));

        var withEmptyMap = run.FreezeEmployeeAuxiliaryAccounts(new Dictionary<Guid, string?>());

        Assert.True(withEmptyMap.IsSuccess);
        Assert.Equal(
            PayrollEmployeeAuxiliaryAccountResolver.Resolve("EMP-8744456"),
            run.Payslips.First().EmployeeAuxiliaryAccount);
    }

    [Fact]
    public void Freeze_WithNullMap_KeepsTheHistoricalDerivation()
    {
        var run = BuildRun(("EMP-9655554", "Sana Chahlaoui"));

        Assert.True(run.FreezeEmployeeAuxiliaryAccounts().IsSuccess);
        Assert.Equal("4259655554", run.Payslips.First().EmployeeAuxiliaryAccount);
    }

    [Fact]
    public void Freeze_AssignedAccounts_ResolveACollisionThatDerivationWouldCause()
    {
        // « 1 » et « 0000001 » dérivent tous deux vers 4250000001 : allouer un compte explicite à
        // l'un des deux débloque la validation, ce que le message d'erreur recommande.
        var run = BuildRun(("1", "Alice"), ("0000001", "Bob"));
        var bob = run.Payslips.Last().EmployeeId;

        var blocked = run.FreezeEmployeeAuxiliaryAccounts();
        Assert.True(blocked.IsFailure);
        Assert.Contains("compte auxiliaire distinct", blocked.Error.Description);

        var unblocked = run.FreezeEmployeeAuxiliaryAccounts(
            new Dictionary<Guid, string?> { [bob] = "4250002" });

        Assert.True(unblocked.IsSuccess, unblocked.IsFailure ? unblocked.Error.Description : null);
        Assert.Equal(
            2,
            run.Payslips.Select(p => p.EmployeeAuxiliaryAccount).Distinct(StringComparer.Ordinal).Count());
    }

    private static Employee BuildEmployee() =>
        Employee.Create("EMP-001", "Karim", "Soumi", new DateTime(2026, 1, 1)).Value;

    private static PayrollRun BuildRun(params (string Number, string Name)[] employees)
    {
        var pars = PayrollParameterDefaults.CreateDefaults(2026).Value;
        var run = PayrollRun.Create(2026, 8, 2026).Value;
        var input = new PayrollComputationInput { BaseSalary = 2000m, Regime = SocialRegime.Rsna };
        var computation = PayrollCalculator.Compute(input, pars);
        var (empRate, employerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, pars);

        run.SetPayslips(employees
            .Select(e => Payslip.FromComputation(
                run.Id, Guid.NewGuid(), e.Name, e.Number, null, 2026, 8, computation, empRate, employerRate))
            .ToList());

        return run;
    }
}
