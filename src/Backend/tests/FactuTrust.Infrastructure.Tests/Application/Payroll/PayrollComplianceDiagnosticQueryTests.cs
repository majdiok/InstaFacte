using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Payroll.Reports;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

/// <summary>
/// Diagnostic conformité paie (plan §5.4) : 7 checks scorés, lecture seule par tenant.
/// </summary>
public sealed class PayrollComplianceDiagnosticQueryTests
{
    private static readonly Guid PeriodId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static void SetEntityId(object entity, Guid id) =>
        typeof(FactuTrust.Domain.Common.Entity).GetProperty(nameof(FactuTrust.Domain.Common.Entity.Id))!.SetValue(entity, id);

    private static (PayrollComplianceDiagnosticQueryHandler Handler, Mock<IJournalEntryRepository> Journal) CreateHandler(
        IReadOnlyList<PayrollRun> runs,
        IReadOnlyList<JournalEntry>? entries = null)
    {
        var runsRepo = new Mock<IPayrollRunRepository>();
        runsRepo.Setup(r => r.ListAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync(runs);

        var journalRepo = new Mock<IJournalEntryRepository>();
        journalRepo.Setup(j => j.ListActiveBySourceTypesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries ?? Array.Empty<JournalEntry>());

        var advancesRepo = new Mock<IEmployeeAdvanceRepository>();
        advancesRepo.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<EmployeeAdvance>());

        var loansRepo = new Mock<IEmployeeLoanRepository>();
        loansRepo.Setup(l => l.ListAllWithInstallmentsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<EmployeeLoan>());

        var employeesRepo = new Mock<IEmployeeRepository>();
        employeesRepo.Setup(e => e.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Employee>());

        var parametersRepo = new Mock<IPayrollParametersRepository>();
        parametersRepo.Setup(p => p.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<PayrollYearParameters>());

        return (new PayrollComplianceDiagnosticQueryHandler(
            runsRepo.Object, journalRepo.Object, advancesRepo.Object, loansRepo.Object, employeesRepo.Object, parametersRepo.Object),
            journalRepo);
    }

    [Fact]
    public async Task EmptyTenant_YieldsScore100_AllChecksPass()
    {
        var (handler, _) = CreateHandler(Array.Empty<PayrollRun>());

        var result = await handler.Handle(new PayrollComplianceDiagnosticQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value.Score);
        Assert.Equal(0, result.Value.IssueCount);
        Assert.Equal(7, result.Value.Checks.Count);
        Assert.All(result.Value.Checks, c => Assert.True(c.Passed));
        Assert.All(result.Value.Checks, c => Assert.Equal(0, c.FindingCount));
    }

    [Fact]
    public async Task JournalEntryWith641IndemnityLine_FlagsMisclassification()
    {
        var run = PayrollRun.Create(2026, 8, 2026).Value;
        SetEntityId(run, Guid.NewGuid());

        // OD legacy déséquilibrée SCE : une indemnité ordinaire en 641 (overlay account), équilibrée par 425.
        var lines = new JournalLineInput[]
        {
            new("641", "Indemnité", 100m, 0m, null, ThirdPartyKind.None),
            new("425", "Personnel à payer", 0m, 100m, null, ThirdPartyKind.None)
        };
        var entry = JournalEntry.Create(1, "JOD", new DateTime(2026, 8, 31), "Paie 08/2026", PeriodId, true,
            "PayrollRun", run.Id, lines, Money.DefaultCurrency).Value;

        var (handler, _) = CreateHandler(new[] { run }, new[] { entry });

        var result = await handler.Handle(new PayrollComplianceDiagnosticQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.IssueCount);
        Assert.True(result.Value.Score < 100);
        var misclass = Assert.Single(result.Value.Checks, c => c.Code == "misclassification");
        Assert.False(misclass.Passed);
        Assert.Equal(1, misclass.FindingCount);
        // L'aperçu de reclassement propose un débit 640 / crédit 641 de 100.
        var finding = Assert.Single(misclass.Findings);
        Assert.Equal(100m, finding.Amounts["debit_640"]);
        Assert.Equal(100m, finding.Amounts["credit_641"]);
    }
}
