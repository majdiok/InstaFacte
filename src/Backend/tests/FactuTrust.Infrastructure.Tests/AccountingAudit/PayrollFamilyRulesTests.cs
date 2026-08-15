using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Audit;
using FactuTrust.Domain.Entities.AccountingAudit;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services.AccountingAudit;
using FactuTrust.Infrastructure.Services.AccountingAudit.Rules;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AccountingAudit;

/// <summary>
/// Famille « Paie » du réviseur.
///
/// <para>Les bulletins sont produits par le vrai moteur de paie (<c>PayrollCalculator</c>), pas par
/// une fabrique de test : les taux, assiettes et montants sont ceux de la production. Les anomalies
/// sont créées en faisant diverger le <b>contrat</b> du bulletin — exactement ce qui arrive en
/// pratique quand un régime est modifié après coup.</para>
/// </summary>
public sealed class PayrollFamilyRulesTests
{
    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;
        public TestTenantDbContextFactory(string databaseName) => _databaseName = databaseName;

        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options;
            return new TenantDbContext(options);
        }
    }

    private const int Year = 2026;
    private const int Month = 3;
    private static int _sequence = 1;

    private static TestTenantDbContextFactory NewFactory(string label) => new($"{label}_{Guid.NewGuid()}");

    private static async Task<IReadOnlyList<AnomalyCandidate>> EvaluateAsync(
        TestTenantDbContextFactory factory, IAccountingAuditRule rule)
    {
        await using var db = factory.CreateContext();
        var ctx = new AuditEvaluationContextImpl(
            db, new AccountingSettings(), Year,
            new DateOnly(Year, 1, 1), new DateOnly(Year, 12, 31),
            Array.Empty<AccountingControlRuleSetting>(), moduleCodesFilter: null);
        return await rule.EvaluateAsync(ctx, CancellationToken.None);
    }

    private static PayrollYearParameters Parameters() =>
        PayrollParameterDefaults.CreateDefaults(Year).Value;

    private static Employee NewEmployee(int dependentParents = 0) =>
        Employee.Create(
            $"EMP-{_sequence++:D3}", "Prénom", "Nom", new DateTime(Year - 2, 1, 1),
            cnssNumber: "12345678",
            dependentParents: dependentParents).Value;

    private static EmploymentContract NewContract(
        Guid employeeId,
        SocialRegime regime,
        decimal baseSalary,
        WeeklyWorkRegime weeklyRegime = WeeklyWorkRegime.FortyEightHours) =>
        EmploymentContract.CreatePublic(
            employeeId,
            regime == SocialRegime.SivpExonere ? ContractType.Sivp : ContractType.Cdi,
            regime,
            new DateTime(Year - 1, 1, 1),
            baseSalary,
            workAccidentRate: 0.4m,
            weeklyRegime: weeklyRegime).Value;

    /// <summary>
    /// Cycle validé d'un bulletin calculé sous <paramref name="computedRegime"/>. Faire diverger ce
    /// régime de celui du contrat crée l'incohérence recherchée.
    /// </summary>
    private static PayrollRun NewValidatedRun(
        Employee employee, decimal baseSalary, SocialRegime computedRegime)
    {
        var parameters = Parameters();
        var computation = PayrollCalculator.Compute(
            new PayrollComputationInput
            {
                BaseSalary = baseSalary,
                Regime = computedRegime,
                WorkAccidentRate = 0.4m
            },
            parameters);

        var (employeeRate, employerRate) = PayrollCalculator.ResolveCnssRates(computedRegime, parameters);

        var run = PayrollRun.Create(Year, Month, Year).Value;
        var payslip = Payslip.FromComputation(
            run.Id, employee.Id, $"{employee.FirstName} {employee.LastName}",
            employee.EmployeeNumber, employee.CnssNumber, Year, Month,
            computation, employeeRate, employerRate);

        run.SetPayslips([payslip]);
        run.Validate("test");
        return run;
    }

    // ── CNSS vs régime du contrat ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Cnss_mismatch_is_silent_when_contract_and_payslip_agree()
    {
        var factory = NewFactory("CnssOk");

        await using (var db = factory.CreateContext())
        {
            var employee = NewEmployee();
            db.Set<Employee>().Add(employee);
            db.Set<EmploymentContract>().Add(NewContract(employee.Id, SocialRegime.Rsna, 1200m));
            db.Set<PayrollYearParameters>().Add(Parameters());
            db.Set<PayrollRun>().Add(NewValidatedRun(employee, 1200m, SocialRegime.Rsna));
            await db.SaveChangesAsync();
        }

        Assert.Empty(await EvaluateAsync(factory, new PayrollCnssRegimeMismatchAuditRule()));
    }

    /// <summary>
    /// Contrat SIVP exonéré, bulletin calculé en RSNA : la CNSS a été retenue à tort sur le
    /// salarié.
    /// </summary>
    [Fact]
    public async Task Cnss_mismatch_flags_a_sivp_contract_charged_with_cnss()
    {
        var factory = NewFactory("CnssSivp");

        await using (var db = factory.CreateContext())
        {
            var employee = NewEmployee();
            db.Set<Employee>().Add(employee);
            db.Set<EmploymentContract>().Add(NewContract(employee.Id, SocialRegime.SivpExonere, 1200m));
            db.Set<PayrollYearParameters>().Add(Parameters());
            db.Set<PayrollRun>().Add(NewValidatedRun(employee, 1200m, SocialRegime.Rsna));
            await db.SaveChangesAsync();
        }

        var anomaly = Assert.Single(await EvaluateAsync(factory, new PayrollCnssRegimeMismatchAuditRule()));
        Assert.Equal((int)PreClosingSeverity.Blocking, anomaly.Severity);
    }

    /// <summary>
    /// Un cycle en brouillon n'a aucun bulletin — <c>SetPayslips</c> le fait passer à « calculé » —
    /// il n'y a donc rien à contrôler. Ce test fixe ce comportement : la règle ne doit pas inventer
    /// d'anomalie à partir d'un cycle vide.
    /// </summary>
    [Fact]
    public async Task Cnss_mismatch_says_nothing_about_a_run_without_payslips()
    {
        var factory = NewFactory("CnssDraft");

        await using (var db = factory.CreateContext())
        {
            var employee = NewEmployee();
            var run = PayrollRun.Create(Year, Month, Year).Value;

            db.Set<Employee>().Add(employee);
            db.Set<EmploymentContract>().Add(NewContract(employee.Id, SocialRegime.SivpExonere, 1200m));
            db.Set<PayrollYearParameters>().Add(Parameters());
            db.Set<PayrollRun>().Add(run);
            await db.SaveChangesAsync();

            Assert.Equal(PayrollRunStatus.Draft, run.Status);
        }

        Assert.Empty(await EvaluateAsync(factory, new PayrollCnssRegimeMismatchAuditRule()));
    }

    /// <summary>
    /// Un cycle simplement <b>calculé</b>, non encore validé, porte de vrais montants : il doit être
    /// contrôlé. Attendre la validation laisserait passer l'erreur jusqu'à ce qu'elle soit figée.
    /// </summary>
    [Fact]
    public async Task Cnss_mismatch_checks_a_calculated_run_before_validation()
    {
        var factory = NewFactory("CnssCalculated");

        await using (var db = factory.CreateContext())
        {
            var employee = NewEmployee();
            var parameters = Parameters();
            var computation = PayrollCalculator.Compute(
                new PayrollComputationInput
                {
                    BaseSalary = 1200m, Regime = SocialRegime.Rsna, WorkAccidentRate = 0.4m
                },
                parameters);
            var (employeeRate, employerRate) =
                PayrollCalculator.ResolveCnssRates(SocialRegime.Rsna, parameters);

            var run = PayrollRun.Create(Year, Month, Year).Value;
            run.SetPayslips([Payslip.FromComputation(
                run.Id, employee.Id, "Prénom Nom", employee.EmployeeNumber, employee.CnssNumber,
                Year, Month, computation, employeeRate, employerRate)]);
            // Volontairement pas de Validate : le cycle reste « calculé ».

            db.Set<Employee>().Add(employee);
            db.Set<EmploymentContract>().Add(NewContract(employee.Id, SocialRegime.SivpExonere, 1200m));
            db.Set<PayrollYearParameters>().Add(parameters);
            db.Set<PayrollRun>().Add(run);
            await db.SaveChangesAsync();

            Assert.Equal(PayrollRunStatus.Calculated, run.Status);
        }

        Assert.Single(await EvaluateAsync(factory, new PayrollCnssRegimeMismatchAuditRule()));
    }

    /// <summary>Sans paramètres d'exercice, aucun taux de référence : la règle s'abstient.</summary>
    [Fact]
    public async Task Cnss_mismatch_abstains_without_year_parameters()
    {
        var factory = NewFactory("CnssNoParams");

        await using (var db = factory.CreateContext())
        {
            var employee = NewEmployee();
            db.Set<Employee>().Add(employee);
            db.Set<EmploymentContract>().Add(NewContract(employee.Id, SocialRegime.SivpExonere, 1200m));
            db.Set<PayrollRun>().Add(NewValidatedRun(employee, 1200m, SocialRegime.Rsna));
            await db.SaveChangesAsync();
        }

        Assert.Empty(await EvaluateAsync(factory, new PayrollCnssRegimeMismatchAuditRule()));
    }

    // ── Parent à charge ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dependent_without_proof_flags_a_counter_with_no_named_claim()
    {
        var factory = NewFactory("ParentIncomplete");

        await using (var db = factory.CreateContext())
        {
            db.Set<Employee>().Add(NewEmployee(dependentParents: 2));
            await db.SaveChangesAsync();
        }

        var anomaly = Assert.Single(await EvaluateAsync(factory, new PayrollDependentWithoutProofAuditRule()));
        Assert.Contains("sans pièce", anomaly.Title);
    }

    [Fact]
    public async Task Dependent_without_proof_is_silent_when_the_parent_is_named()
    {
        var factory = NewFactory("ParentComplete");

        await using (var db = factory.CreateContext())
        {
            var employee = NewEmployee(dependentParents: 1);
            db.Set<Employee>().Add(employee);
            db.Set<EmployeeDependentParent>().Add(EmployeeDependentParent.Create(
                employee.Id, "12345678", DependentParentKinship.Father, new DateTime(Year, 1, 1)).Value);
            await db.SaveChangesAsync();
        }

        Assert.Empty(await EvaluateAsync(factory, new PayrollDependentWithoutProofAuditRule()));
    }

    /// <summary>
    /// Le même CIN parent réclamé par deux salariés : un seul peut prétendre à la déduction,
    /// l'IRPP a donc été sous-retenu chez l'autre.
    /// </summary>
    [Fact]
    public async Task Dependent_without_proof_flags_a_parent_claimed_twice()
    {
        var factory = NewFactory("ParentConflict");

        await using (var db = factory.CreateContext())
        {
            var first = NewEmployee(dependentParents: 1);
            var second = NewEmployee(dependentParents: 1);
            db.Set<Employee>().AddRange(first, second);
            db.Set<EmployeeDependentParent>().AddRange(
                EmployeeDependentParent.Create(
                    first.Id, "87654321", DependentParentKinship.Mother, new DateTime(Year, 1, 1)).Value,
                EmployeeDependentParent.Create(
                    second.Id, "87654321", DependentParentKinship.Mother, new DateTime(Year, 1, 1)).Value);
            await db.SaveChangesAsync();
        }

        var found = await EvaluateAsync(factory, new PayrollDependentWithoutProofAuditRule());

        var conflict = Assert.Single(found, a => a.Title.Contains("plusieurs salariés"));
        Assert.Equal((int)PreClosingSeverity.Blocking, conflict.Severity);
    }

    // ── Heures supplémentaires hors régime ────────────────────────────────────────────────

    [Fact]
    public async Task Overtime_is_silent_on_a_legal_rate_for_the_regime()
    {
        var factory = NewFactory("OvertimeOk");

        await using (var db = factory.CreateContext())
        {
            var employee = NewEmployee();
            db.Set<Employee>().Add(employee);
            db.Set<EmploymentContract>().Add(
                NewContract(employee.Id, SocialRegime.Rsna, 1200m, WeeklyWorkRegime.FortyEightHours));
            // 175 % est le taux légal du régime 48 h.
            db.Set<PayrollOvertimeLine>().Add(PayrollOvertimeLine.Create(
                employee.Id, Year, Month, hours: 10m, ratePercent: 175m, baseSalary: 1200m,
                weeklyRegime: WeeklyWorkRegime.FortyEightHours).Value);
            await db.SaveChangesAsync();
        }

        Assert.Empty(await EvaluateAsync(factory, new PayrollOvertimeOutOfRegimeAuditRule()));
    }

    [Fact]
    public async Task Overtime_flags_a_rate_incompatible_with_the_weekly_regime()
    {
        var factory = NewFactory("OvertimeBad");

        await using (var db = factory.CreateContext())
        {
            var employee = NewEmployee();
            db.Set<Employee>().Add(employee);
            // Contrat 40 h : le 175 % n'a pas de fondement sans option « taux étendus ».
            db.Set<EmploymentContract>().Add(
                NewContract(employee.Id, SocialRegime.Rsna, 1200m, WeeklyWorkRegime.FortyHours));
            db.Set<PayrollOvertimeLine>().Add(PayrollOvertimeLine.Create(
                employee.Id, Year, Month, hours: 10m, ratePercent: 175m, baseSalary: 1200m,
                enableExtendedOvertimeRates: true).Value);
            await db.SaveChangesAsync();
        }

        var anomaly = Assert.Single(await EvaluateAsync(factory, new PayrollOvertimeOutOfRegimeAuditRule()));
        Assert.Contains("hors régime", anomaly.Title);
    }

    // ── SMIG ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Below_smig_flags_a_full_time_salary_under_the_minimum()
    {
        var factory = NewFactory("SmigBelow");
        var parameters = Parameters();

        await using (var db = factory.CreateContext())
        {
            var employee = NewEmployee();
            db.Set<Employee>().Add(employee);
            db.Set<PayrollYearParameters>().Add(parameters);
            // Un brut très inférieur au SMIG de l'exercice.
            db.Set<PayrollRun>().Add(NewValidatedRun(employee, 50m, SocialRegime.Rsna));
            await db.SaveChangesAsync();
        }

        // Le contrôle n'a de sens que si l'exercice porte un SMIG.
        if (parameters.MonthlySmig <= 0) return;

        var anomaly = Assert.Single(await EvaluateAsync(factory, new PayrollBelowSmigAuditRule()));
        Assert.Equal((int)PreClosingSeverity.Blocking, anomaly.Severity);
    }

    [Fact]
    public async Task Below_smig_is_silent_on_a_salary_above_the_minimum()
    {
        var factory = NewFactory("SmigOk");
        var parameters = Parameters();

        await using (var db = factory.CreateContext())
        {
            var employee = NewEmployee();
            db.Set<Employee>().Add(employee);
            db.Set<PayrollYearParameters>().Add(parameters);
            db.Set<PayrollRun>().Add(NewValidatedRun(
                employee, Math.Max(parameters.MonthlySmig, 500m) + 1000m, SocialRegime.Rsna));
            await db.SaveChangesAsync();
        }

        Assert.Empty(await EvaluateAsync(factory, new PayrollBelowSmigAuditRule()));
    }

    /// <summary>Aucun SMIG paramétré : aucun seuil légal n'est codé en dur, la règle s'abstient.</summary>
    [Fact]
    public async Task Below_smig_abstains_without_a_configured_minimum()
    {
        var factory = NewFactory("SmigNoParams");

        await using (var db = factory.CreateContext())
        {
            var employee = NewEmployee();
            db.Set<Employee>().Add(employee);
            db.Set<PayrollRun>().Add(NewValidatedRun(employee, 50m, SocialRegime.Rsna));
            await db.SaveChangesAsync();
        }

        Assert.Empty(await EvaluateAsync(factory, new PayrollBelowSmigAuditRule()));
    }
}
