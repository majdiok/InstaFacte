using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

/// <summary>Vue détail enrichie (T10/D8) : parité avec GET /{id} + indicateurs calculés.</summary>
public sealed class RecurringContractDetailTests
{
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    [Fact]
    public async Task Detail_ReturnsAllBaseFields_MatchingGetAsync()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client détail");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), endDate: new DateTime(2026, 12, 31),
            frequency: BillingFrequency.Quarterly, billingDay: 5, autoRenew: false,
            noticePeriodDays: 60, reference: "REF-DETAIL", notes: "Note de test",
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 2, 150m, 19m));

        await using var sut = harness.CreateService();
        var baseDto = await sut.GetAsync(contract.Id);
        var detail = await sut.GetDetailAsync(contract.Id);

        Assert.NotNull(baseDto);
        Assert.NotNull(detail);

        // Parité champ à champ avec GET /{id} (le DTO existant reste la référence).
        Assert.Equal(baseDto!.Id, detail!.Id);
        Assert.Equal(baseDto.Number, detail.Number);
        Assert.Equal(baseDto.ClientId, detail.ClientId);
        Assert.Equal(baseDto.ClientName, detail.ClientName);
        Assert.Equal(baseDto.Status, detail.Status);
        Assert.Equal(baseDto.StatusDisplay, detail.StatusDisplay);
        Assert.Equal(baseDto.BillingFrequency, detail.BillingFrequency);
        Assert.Equal(baseDto.BillingFrequencyDisplay, detail.BillingFrequencyDisplay);
        Assert.Equal(baseDto.BillingDayOfMonth, detail.BillingDayOfMonth);
        Assert.Equal(baseDto.StartDate, detail.StartDate);
        Assert.Equal(baseDto.EndDate, detail.EndDate);
        Assert.Equal(baseDto.NextBillingDate, detail.NextBillingDate);
        Assert.Equal(baseDto.LastBilledPeriodEnd, detail.LastBilledPeriodEnd);
        Assert.Equal(baseDto.PaymentTermTemplateId, detail.PaymentTermTemplateId);
        Assert.Equal(baseDto.AutoRenew, detail.AutoRenew);
        Assert.Equal(baseDto.NoticePeriodDays, detail.NoticePeriodDays);
        Assert.Equal(baseDto.Currency, detail.Currency);
        Assert.Equal(baseDto.SourceQuoteId, detail.SourceQuoteId);
        Assert.Equal(baseDto.Reference, detail.Reference);
        Assert.Equal(baseDto.Notes, detail.Notes);
        Assert.Equal(baseDto.SetupFeeBilled, detail.SetupFeeBilled);
        Assert.Equal(baseDto.Lines.Count, detail.Lines.Count);
        Assert.Equal(baseDto.Lines[0].Id, detail.Lines[0].Id);
        Assert.Equal(baseDto.Lines[0].Description, detail.Lines[0].Description);
    }

    [Fact]
    public async Task Detail_CancellationDeadline_IsEndDateMinusNoticePeriod()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client deadline");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), endDate: new DateTime(2026, 12, 31),
            noticePeriodDays: 30,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        await using var sut = harness.CreateService();
        var detail = await sut.GetDetailAsync(contract.Id);

        Assert.NotNull(detail);
        Assert.Equal(new DateTime(2026, 12, 1), detail!.CancellationDeadline);
    }

    [Fact]
    public async Task Detail_CancellationDeadline_NullWhenNoEndDate()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client sans deadline");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), endDate: null,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        await using var sut = harness.CreateService();
        var detail = await sut.GetDetailAsync(contract.Id);

        Assert.NotNull(detail);
        Assert.Null(detail!.CancellationDeadline);
    }

    [Fact]
    public async Task Detail_UpcomingOccurrencesCount_ZeroWhenCancelled()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client annulé");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), endDate: new DateTime(2026, 12, 31),
            configure: c =>
            {
                c.Activate();
                c.Cancel();
            },
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        await using var sut = harness.CreateService();
        var detail = await sut.GetDetailAsync(contract.Id);

        Assert.NotNull(detail);
        Assert.Equal(RecurringContractStatus.Cancelled, detail!.Status);
        Assert.Equal(0, detail.UpcomingOccurrencesCount);
    }

    [Fact]
    public async Task Detail_UpcomingOccurrencesCount_PositiveWhenActive()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client occurrences");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), endDate: null,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        await using var sut = harness.CreateService();
        var detail = await sut.GetDetailAsync(contract.Id);

        Assert.NotNull(detail);
        // Fenêtre glissante de 12 mois → au moins une occurrence à venir.
        Assert.InRange(detail!.UpcomingOccurrencesCount, 1, 12);
    }

    [Fact]
    public async Task Detail_CurrentPeriodTotals_IncludeUnbilledSetupFee_ExcludeUsageLines()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client totaux");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), // lignes effectives depuis le 01/01 → actives aujourd'hui
            configure: c => c.Activate(),
            lines: c =>
            {
                c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m);
                c.AddLine(RecurringContractLineType.OneTimeSetup, "Installation", 1, 50m, 19m);
                c.AddLine(RecurringContractLineType.UsageMetered, "Consommation", 1, 500m, 19m,
                    usageMetricId: Guid.NewGuid());
            });

        await using var sut = harness.CreateService();
        var detail = await sut.GetDetailAsync(contract.Id);

        Assert.NotNull(detail);
        // HT = 100 (fixe) + 50 (setup non facturé) ; la ligne usage (500) est exclue.
        Assert.Equal(150m, detail!.CurrentPeriodTotalHT);
        Assert.Equal(28.5m, detail.CurrentPeriodTotalTVA);
        Assert.Equal(178.5m, detail.CurrentPeriodTotalTTC);
    }

    [Fact]
    public async Task Detail_CurrentPeriodTotals_ExcludeSetupFee_WhenAlreadyBilled()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client setup facturé");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1),
            configure: c =>
            {
                c.Activate();
                c.MarkSetupFeeBilled();
            },
            lines: c =>
            {
                c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m);
                c.AddLine(RecurringContractLineType.OneTimeSetup, "Installation", 1, 50m, 19m);
            });

        await using var sut = harness.CreateService();
        var detail = await sut.GetDetailAsync(contract.Id);

        Assert.NotNull(detail);
        Assert.Equal(100m, detail!.CurrentPeriodTotalHT);
        Assert.Equal(19m, detail.CurrentPeriodTotalTVA);
        Assert.Equal(119m, detail.CurrentPeriodTotalTTC);
    }

    [Fact]
    public async Task Detail_EstimatedMonthlyAmount_NormalizesQuarterlyAndAnnual()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client mrr");
        var quarterly = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), frequency: BillingFrequency.Quarterly,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Trimestre", 1, 300m, 19m));
        var annual = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), frequency: BillingFrequency.Annual,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Année", 1, 1200m, 19m));

        await using var sut = harness.CreateService();
        var quarterlyDetail = await sut.GetDetailAsync(quarterly.Id);
        var annualDetail = await sut.GetDetailAsync(annual.Id);

        Assert.NotNull(quarterlyDetail);
        Assert.NotNull(annualDetail);
        Assert.Equal(100m, quarterlyDetail!.EstimatedMonthlyAmount); // 300 / 3
        Assert.Equal(100m, annualDetail!.EstimatedMonthlyAmount);    // 1200 / 12
    }

    [Fact]
    public async Task Detail_UnknownContract_ReturnsNull()
    {
        var harness = new RecurringContractTestHarness();
        await using var sut = harness.CreateService();

        var detail = await sut.GetDetailAsync(Guid.NewGuid());

        Assert.Null(detail);
    }
}
