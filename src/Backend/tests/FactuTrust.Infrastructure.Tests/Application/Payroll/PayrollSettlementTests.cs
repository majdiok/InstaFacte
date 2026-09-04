using System.Reflection;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Payroll.Commands;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

/// <summary>
/// R-06 : le solde à la validation se cale strictement sur les lignes de déduction figées du
/// bulletin (SourceEntityId) — plus de solde forfaitaire tenant-wide. Garde-fou anti-staleness.
/// </summary>
public sealed class PayrollSettlementTests
{
    private static readonly Guid EmpId = Guid.Parse("eeeeeeee-1111-1111-1111-111111111111");
    private static readonly DateTime CalcTime = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);

    private static void SetProp(object entity, string name, object? value) =>
        entity.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?
            .SetValue(entity, value);

    private static PayrollYearParameters Params() => PayrollParameterDefaults.CreateDefaults(2026).Value;

    /// <summary>Construit un cycle Calculated 08/2026 avec une seule ligne de déduction figée.</summary>
    private static PayrollRun BuildRunWithDeductionLine(DeductionLineInput deductionLine)
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m,
            DeductionLines = new[] { deductionLine }
        };
        var computation = PayrollCalculator.Compute(input, Params());
        var run = PayrollRun.Create(2026, 8, 2026).Value;
        var (empRate, employerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, Params());
        var payslip = Payslip.FromComputation(
            run.Id, EmpId, "Test User", "EMP-001", null, 2026, 8, computation, empRate, employerRate);
        run.SetPayslips([payslip]);
        SetProp(run, "CalculatedAt", CalcTime);
        return run;
    }

    private static PayrollRun BuildRunWithPostTaxDeductionLine(DeductionLineInput postTaxLine)
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = 2000m,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m,
            PostTaxDeductionLines = new[] { postTaxLine }
        };
        var computation = PayrollCalculator.Compute(input, Params());
        var run = PayrollRun.Create(2026, 8, 2026).Value;
        var (empRate, employerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, Params());
        var payslip = Payslip.FromComputation(
            run.Id, EmpId, "Test User", "EMP-001", null, 2026, 8, computation, empRate, employerRate);
        run.SetPayslips([payslip]);
        SetProp(run, "CalculatedAt", CalcTime);
        return run;
    }

    private static (ValidatePayrollRunCommandHandler handler, Mock<IEmployeeAdvanceRepository> advances,
        Mock<IEmployeeLoanRepository> loans, Mock<IEmployeeGarnishmentRepository> garnishments)
        CreateHandler(PayrollRun run)
    {
        var runs = new Mock<IPayrollRunRepository>();
        runs.Setup(r => r.GetByIdWithPayslipsAsync(run.Id, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        runs.Setup(r => r.UpdateScalarAsync(run, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var advances = new Mock<IEmployeeAdvanceRepository>();
        advances.Setup(a => a.ListOutstandingByEmployeeIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmployeeAdvance>());
        advances.Setup(a => a.UpdateRangeAsync(It.IsAny<IReadOnlyList<EmployeeAdvance>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var leaves = new Mock<ILeaveRequestRepository>();
        leaves.Setup(l => l.ListForMonthAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LeaveRequest>());

        var accruals = new Mock<ILeaveBalanceAccrualRepository>();
        accruals.Setup(a => a.GetByEmployeePeriodsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LeaveBalanceAccrual>());
        accruals.Setup(a => a.AddRangeAsync(It.IsAny<IReadOnlyList<LeaveBalanceAccrual>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var loans = new Mock<IEmployeeLoanRepository>();
        loans.Setup(l => l.ListWithDueInstallmentsForMonthAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmployeeLoan>());
        loans.Setup(l => l.UpdateRangeAsync(It.IsAny<IReadOnlyList<EmployeeLoan>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var garnishments = new Mock<IEmployeeGarnishmentRepository>();
        garnishments.Setup(g => g.ListActiveForEmployeesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmployeeGarnishment>());
        garnishments.Setup(g => g.UpdateRangeAsync(It.IsAny<IReadOnlyList<EmployeeGarnishment>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var parameters = new Mock<IPayrollParametersRepository>();
        parameters.Setup(p => p.GetOrCreateForYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(Params());

        var accounting = new Mock<IAccountingService>();
        accounting.Setup(a => a.GeneratePayrollRunEntryAsync(run, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        var uow = new Mock<ITenantUnitOfWork>();
        uow.Setup(u => u.ExecuteAsync(It.IsAny<Func<CancellationToken, Task<Result>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<Result>>, CancellationToken>((action, ct) => action(ct));

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(c => c.Email).Returns("tester@firm.tn");
        currentUser.SetupGet(c => c.TenantId).Returns((Guid?)Guid.NewGuid());

        var collaboratorCostSync = new Mock<IFirmCollaboratorCostSyncService>();

        // La dérivation par troncature du matricule ayant été supprimée, le figeage exige un compte
        // auxiliaire sur la fiche. Le salarié du cycle en porte donc un, déjà alloué.
        var employee = Employee.Create("EMP-001", "Test", "User", new DateTime(2026, 1, 1)).Value;
        SetProp(employee, "Id", EmpId);
        Assert.True(employee.SetAuxiliaryAccountNumber("4250001").IsSuccess);

        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(e => e.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Employee> { [EmpId] = employee });

        // Aucune allocation ne doit être nécessaire : le compte est déjà là.
        var chartProvisioning = new Mock<IPayrollEmployeeChartProvisioningService>(MockBehavior.Strict);

        var handler = new ValidatePayrollRunCommandHandler(
            runs.Object, advances.Object, leaves.Object, accruals.Object, loans.Object, garnishments.Object,
            employees.Object, chartProvisioning.Object, parameters.Object, accounting.Object,
            uow.Object, currentUser.Object,
            collaboratorCostSync.Object,
            Options.Create(new FirmGovernanceOptions { AutoImportOnPayrollValidate = false }),
            Options.Create(new AccountingSettings { PayrollStrictSettlementEnabled = true }),
            NullLogger<ValidatePayrollRunCommandHandler>.Instance);

        return (handler, advances, loans, garnishments);
    }

    [Fact]
    public async Task StrictSettlement_SettlesOnlyAdvancesReferencedByFrozenLines()
    {
        // Avance A (100) référencée par le bulletin ; avance B (200) non référencée, antérieure au calcul.
        var advanceA = EmployeeAdvance.Create(EmpId, new DateTime(2026, 7, 1), 100m).Value;
        var advanceB = EmployeeAdvance.Create(EmpId, new DateTime(2026, 8, 1), 200m).Value;
        SetProp(advanceA, "CreatedAt", CalcTime.AddSeconds(-60));
        SetProp(advanceB, "CreatedAt", CalcTime.AddSeconds(-60));

        var run = BuildRunWithDeductionLine(
            new DeductionLineInput("Avance sur salaire", 100m, DeductionKind.Advance, SourceEntityId: advanceA.Id));

        var (handler, advances, _, _) = CreateHandler(run);
        var outstanding = new List<EmployeeAdvance> { advanceA, advanceB };
        advances.Setup(a => a.ListOutstandingByEmployeeIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outstanding);

        var result = await handler.Handle(new ValidatePayrollRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(run.Id, advanceA.SettledInPayrollRunId);     // retenue figée → soldée
        Assert.Null(advanceB.SettledInPayrollRunId);              // non référencée → non soldée (R-06)
    }

    [Fact]
    public async Task StrictSettlement_StaleAdvanceBlocksValidation()
    {
        // Avance créée APRÈS le calcul (CreatedAt > CalculatedAt) → staleness, validation bloquée.
        var staleAdvance = EmployeeAdvance.Create(EmpId, new DateTime(2026, 8, 20), 150m).Value;
        SetProp(staleAdvance, "CreatedAt", CalcTime.AddSeconds(60));

        var run = BuildRunWithDeductionLine(
            new DeductionLineInput("Avance sur salaire", 100m, DeductionKind.Advance, SourceEntityId: staleAdvance.Id));

        var (handler, advances, _, _) = CreateHandler(run);
        advances.Setup(a => a.ListOutstandingByEmployeeIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance> { staleAdvance });

        var result = await handler.Handle(new ValidatePayrollRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("StaleDeductions", result.Error.Code);
        Assert.Contains("recalculez le cycle", result.Error.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Null(staleAdvance.SettledInPayrollRunId); // rien n'a été soldé
    }

    [Fact]
    public async Task StrictSettlement_SettlesOnlyLoanInstallmentsReferencedByFrozenLines()
    {
        // Deux prêts, chacun avec une échéance due en 08/2026. Seul le prêt 1 est référencé par le bulletin.
        var loan1 = EmployeeLoan.Create(EmpId, "L1", 600m, 2, 2026, 8).Value;
        var loan2 = EmployeeLoan.Create(EmpId, "L2", 600m, 2, 2026, 8).Value;
        SetProp(loan1, "CreatedAt", CalcTime.AddSeconds(-60));
        SetProp(loan2, "CreatedAt", CalcTime.AddSeconds(-60));
        // Le garde-fou anti-staleness lit CreatedAt sur l'échéance (entité propre), pas sur le prêt.
        foreach (var i in loan1.Installments) SetProp(i, "CreatedAt", CalcTime.AddSeconds(-60));
        foreach (var i in loan2.Installments) SetProp(i, "CreatedAt", CalcTime.AddSeconds(-60));
        var dueInstallment1 = loan1.Installments.First(i => i.Year == 2026 && i.Month == 8);
        var dueInstallment2 = loan2.Installments.First(i => i.Year == 2026 && i.Month == 8);

        var run = BuildRunWithDeductionLine(
            new DeductionLineInput($"Prêt L1 — échéance {dueInstallment1.SequenceNumber}", dueInstallment1.Amount,
                DeductionKind.Loan, SourceEntityId: dueInstallment1.Id));

        var (handler, _, loans, _) = CreateHandler(run);
        loans.Setup(l => l.ListWithDueInstallmentsForMonthAsync(2026, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeLoan> { loan1, loan2 });

        var result = await handler.Handle(new ValidatePayrollRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.True(dueInstallment1.IsSettled);   // référencée → soldée
        Assert.False(dueInstallment2.IsSettled);  // non référencée → non soldée (R-06)
    }

    [Fact]
    public async Task StrictSettlement_RecordsGarnishmentInstallmentFromFrozenLineAmounts()
    {
        var garnishment = EmployeeGarnishment.Create(
            EmpId, GarnishmentType.Garnishment, "SAI-1", new DateTime(2026, 1, 1), "Créancier X", "RIB1", 1,
            GarnishmentAmountKind.FixedAmount, fixedAmount: 60m, percentOfNet: null, totalAmountDue: 60m,
            startDate: new DateTime(2026, 1, 1)).Value;
        SetProp(garnishment, "CreatedAt", CalcTime.AddSeconds(-60));

        // Ligne figée : 50 saisis (appliqué), 60 demandé, 10 reporté.
        var run = BuildRunWithPostTaxDeductionLine(
            new DeductionLineInput("Saisie — Créancier X", 50m, DeductionKind.Garnishment,
                SourceEntityId: garnishment.Id, RequestedAmount: 60m, CarriedOverAmount: 10m));

        var (handler, _, _, garnishments) = CreateHandler(run);
        garnishments.Setup(g => g.ListActiveForEmployeesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeGarnishment> { garnishment });

        var result = await handler.Handle(new ValidatePayrollRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var installment = Assert.Single(garnishment.Installments);
        Assert.Equal(run.Id, installment.PayrollRunId);
        Assert.Equal(2026, installment.Year);
        Assert.Equal(8, installment.Month);
        Assert.Equal(60m, installment.RequestedAmount);
        Assert.Equal(50m, installment.AppliedAmount);
        Assert.Equal(10m, installment.CarriedOverAmount);
    }

    [Fact]
    public async Task StrictSettlement_RunWithoutLoanLine_SettlesNoInstallments()
    {
        // H1 : un cycle sans aucune ligne de prêt ne solde aucune échéance — plus de solde forfaitaire
        // tenant-wide. L'ancien code soldait toutes les échéances dues du mois dès qu'aucune ligne ne
        // portait de SourceEntityId (y compris pour des salariés sans retenue de prêt).
        var loan = EmployeeLoan.Create(EmpId, "L1", 600m, 2, 2026, 8).Value;
        SetProp(loan, "CreatedAt", CalcTime.AddSeconds(-60));
        foreach (var i in loan.Installments) SetProp(i, "CreatedAt", CalcTime.AddSeconds(-60));
        var dueInstallment = loan.Installments.First(i => i.Year == 2026 && i.Month == 8);

        // Le bulletin ne retient AUCUN prêt (ligne d'avance seulement) → hasLoanDeductionLines = false.
        var run = BuildRunWithDeductionLine(
            new DeductionLineInput("Avance sur salaire", 100m, DeductionKind.Advance, SourceEntityId: Guid.NewGuid()));

        var (handler, _, loans, _) = CreateHandler(run);
        loans.Setup(l => l.ListWithDueInstallmentsForMonthAsync(2026, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeLoan> { loan });

        var result = await handler.Handle(new ValidatePayrollRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.False(dueInstallment.IsSettled); // aucune ligne de prêt → rien soldé (H1)
    }

    [Fact]
    public async Task StrictSettlement_LegacyLoanLineWithoutSource_MatchesByEmployeeAndAmount()
    {
        // H1 repli legacy : lignes de prêt sans SourceEntityId (bulletins antérieurs au typage). On
        // solde par correspondance salarié + montant — jamais toutes les échéances dues du tenant.
        var loan1 = EmployeeLoan.Create(EmpId, "L1", 600m, 2, 2026, 8).Value; // échéance 300
        var loan2 = EmployeeLoan.Create(EmpId, "L2", 400m, 2, 2026, 8).Value; // échéance 200 (sans ligne)
        SetProp(loan1, "CreatedAt", CalcTime.AddSeconds(-60));
        SetProp(loan2, "CreatedAt", CalcTime.AddSeconds(-60));
        foreach (var i in loan1.Installments) SetProp(i, "CreatedAt", CalcTime.AddSeconds(-60));
        foreach (var i in loan2.Installments) SetProp(i, "CreatedAt", CalcTime.AddSeconds(-60));
        var due1 = loan1.Installments.First(i => i.Year == 2026 && i.Month == 8);
        var due2 = loan2.Installments.First(i => i.Year == 2026 && i.Month == 8);

        // Ligne figée sans SourceEntityId, montant = échéance 1 (300). Seule loan1 correspond.
        var run = BuildRunWithDeductionLine(
            new DeductionLineInput($"Prêt L1 — échéance {due1.SequenceNumber}", due1.Amount, DeductionKind.Loan));

        var (handler, _, loans, _) = CreateHandler(run);
        loans.Setup(l => l.ListWithDueInstallmentsForMonthAsync(2026, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeLoan> { loan1, loan2 });

        var result = await handler.Handle(new ValidatePayrollRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.True(due1.IsSettled);  // montant correspondant → soldée (repli legacy)
        Assert.False(due2.IsSettled); // aucune ligne de 200 → non soldée (pas de solde forfaitaire)
    }

    [Fact]
    public async Task StrictSettlement_PartiallySettledAdvance_DoesNotStarveNextAdvance()
    {
        // M2 : une avance déjà partiellement soldée (RemainingAmount < Amount) ne doit pas exiger sa
        // totalité pour être soldée, ni avaler le budget disponible au détriment de l'avance suivante.
        // Avance A (500, déjà soldée de 300 → reliquat 200) ; avance B (300, non soldée). Retenue 350.
        // Ancien code (seuil sur Amount) : SettlePartial(350) échouait sur A (350 > reliquat 200) → A
        // non soldée, B affamée, budget perdu. Nouveau code (seuil sur RemainingAmount) : A soldée
        // (200), B partiellement soldée (150/300).
        var advanceA = EmployeeAdvance.Create(EmpId, new DateTime(2026, 7, 1), 500m).Value;
        var advanceB = EmployeeAdvance.Create(EmpId, new DateTime(2026, 8, 1), 300m).Value;
        Assert.True(advanceA.SettlePartial(Guid.NewGuid(), 300m).IsSuccess); // reliquat 200
        SetProp(advanceA, "CreatedAt", CalcTime.AddSeconds(-60));
        SetProp(advanceB, "CreatedAt", CalcTime.AddSeconds(-60));

        var run = BuildRunWithDeductionLine(
            new DeductionLineInput("Avance sur salaire", 350m, DeductionKind.Advance, SourceEntityId: advanceA.Id));

        var (handler, advances, _, _) = CreateHandler(run);
        advances.Setup(a => a.ListOutstandingByEmployeeIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeAdvance> { advanceA, advanceB });

        var result = await handler.Handle(new ValidatePayrollRunCommand(run.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.True(advanceA.IsSettled);          // reliquat 200 couvert → soldée
        Assert.Equal(500m, advanceA.SettledAmount);
        Assert.False(advanceB.IsSettled);         // partiellement soldée (150/300)
        Assert.Equal(150m, advanceB.SettledAmount);
        Assert.Equal(150m, advanceB.RemainingAmount); // 300 − 150 soldé = 150 restant dû
    }
}
