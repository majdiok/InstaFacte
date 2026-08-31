using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// OD de reclassement SCE paie (plan §5.2.1 / WS-5) : équilibrée, idempotente, refus au second appel.
/// </summary>
public sealed class AccountingServicePayrollReclassificationTests
{
    private static readonly Guid PeriodId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static ChartOfAccount Acc(string number) =>
        ChartOfAccount.Create(number, $"Compte {number}", int.Parse(number[..1].ToString()), null, AccountNatureType.Debit).Value;

    private static PayrollRun BuildRun()
    {
        var pars = PayrollParameterDefaults.CreateDefaults(2026).Value;
        var input = new PayrollComputationInput { BaseSalary = 2000m, Regime = SocialRegime.Rsna, WorkAccidentRate = 0.4m };
        var computation = PayrollCalculator.Compute(input, pars);
        var run = PayrollRun.Create(2026, 8, 2026).Value;
        var (empRate, employerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, pars);
        var payslip = Payslip.FromComputation(run.Id, Guid.NewGuid(), "Test User", "EMP-001", null, 2026, 8, computation, empRate, employerRate);
        run.SetPayslips([payslip]);
        return run;
    }

    private static JournalEntry BuildLegacyEntry(Guid runId)
    {
        // OD legacy sans 641 ni 421 : seules les corrections TFP/FOPROLOS/CSS seront émises.
        var lines = new JournalLineInput[]
        {
            new("640", "Salaire brut", 100m, 0m, null, ThirdPartyKind.None),
            new("425", "Personnel à payer", 0m, 100m, null, ThirdPartyKind.None)
        };
        return JournalEntry.Create(1, "JOD", new DateTime(2026, 8, 31), "Paie 08/2026", PeriodId, true,
            AccountingService.SourcePayrollRun, runId, lines, Money.DefaultCurrency).Value;
    }

    private static (AccountingService Service, List<JournalEntry> Captured, Mock<IJournalEntryRepository> Journals) BuildService(
        PayrollRun run, JournalEntry legacy)
    {
        var chart = new Mock<IChartOfAccountRepository>();
        chart.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(50);
        chart.Setup(x => x.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string n, CancellationToken _) => Acc(n));

        var periodService = new Mock<IAccountingPeriodService>();
        periodService.Setup(x => x.EnsureOpenPeriodAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime d, CancellationToken _) =>
                Result.Success(AccountingPeriod.Create(d.Year, d.Month, new DateTime(d.Year, d.Month, 1),
                    new DateTime(d.Year, d.Month, 1).AddMonths(1).AddDays(-1))));

        var captured = new List<JournalEntry>();
        var journals = new Mock<IJournalEntryRepository>();
        // Garde idempotente reclassement : null tant que rien n'a été créé, puis l'écriture capturée.
        journals.Setup(x => x.GetActiveBySourceAsync(AccountingService.SourcePayrollReclassification, run.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => captured.FirstOrDefault());
        // OD legacy lue pour les montants 641/421.
        journals.Setup(x => x.GetActiveBySourceAsync(AccountingService.SourcePayrollRun, run.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(legacy);
        journals.Setup(x => x.ReserveNextEntryNumberAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        journals.Setup(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
            .Callback<JournalEntry, CancellationToken>((e, _) => captured.Add(e))
            .ReturnsAsync((JournalEntry e, CancellationToken _) => e);

        var withholding = new Mock<IWithholdingTaxRepository>();
        var categories = new Mock<IDepreciationRateCategoryRepository>();
        var ctxFactory = new Mock<ITenantDbContextFactory>();
        var settings = Options.Create(new AccountingSettings { BrouillardEnabled = false });

        var service = new AccountingService(
            chart.Object, periodService.Object, journals.Object, withholding.Object, categories.Object,
            ctxFactory.Object, NullLogger<AccountingService>.Instance, settings);

        return (service, captured, journals);
    }

    [Fact]
    public async Task GeneratesBalancedReclassification_WithTaxCorrectionLines()
    {
        var run = BuildRun();
        var legacy = BuildLegacyEntry(run.Id);
        var (service, captured, _) = BuildService(run, legacy);

        var result = await service.GeneratePayrollReclassificationEntryAsync(run, CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var entry = Assert.Single(captured);
        // Équilibrée (tolérance 0 — arrondi 3 dp).
        Assert.Equal(0m, Math.Round(entry.Lines.Sum(l => l.DebitAmount.Amount) - entry.Lines.Sum(l => l.CreditAmount.Amount), 3));
        Assert.Equal(run.TotalTfp, entry.Lines.Where(l => l.AccountNumber == "6611").Sum(l => l.DebitAmount.Amount));
        Assert.Equal(run.TotalFoprolos, entry.Lines.Where(l => l.AccountNumber == "6612").Sum(l => l.DebitAmount.Amount));
        // 647 crédité du TFP + FOPROLOS (retrait du bucket charges patronales).
        Assert.Equal(run.TotalTfp + run.TotalFoprolos, entry.Lines.Where(l => l.AccountNumber == "647").Sum(l => l.CreditAmount.Amount));
        var taxes = run.TotalTfp + run.TotalFoprolos + run.TotalCssEmployer;
        Assert.Equal(taxes, entry.Lines.Where(l => l.AccountNumber == "432").Sum(l => l.DebitAmount.Amount));
        Assert.Equal(taxes, entry.Lines.Where(l => l.AccountNumber == "437").Sum(l => l.CreditAmount.Amount));
        // Source d'idempotence + pièce référencée.
        Assert.Equal(AccountingService.SourcePayrollReclassification, entry.SourceEntityType);
        Assert.Equal(run.Id, entry.SourceEntityId);
    }

    [Fact]
    public async Task RefusesSecondReclassification_WhileActiveEntryExists()
    {
        var run = BuildRun();
        var legacy = BuildLegacyEntry(run.Id);
        var (service, _, _) = BuildService(run, legacy);

        var first = await service.GeneratePayrollReclassificationEntryAsync(run, CancellationToken.None);
        Assert.True(first.IsSuccess);

        var second = await service.GeneratePayrollReclassificationEntryAsync(run, CancellationToken.None);

        Assert.True(second.IsFailure);
        Assert.Equal("Validation.ReclassementPayrollRun", second.Error.Code);
    }
}
