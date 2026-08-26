using System.Globalization;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

/// <summary>Série mensuelle des montants facturés (T9/D9) : groupement par mois de PeriodTo.</summary>
public sealed class RecurringContractEvolutionTests
{
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    private static string MonthKey(DateTime date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    [Fact]
    public async Task Evolution_GroupsInvoicedRunsByPeriodToMonth()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client évolution");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        // Fenêtre de 6 mois : firstMonth … mois courant. Runs sans facture → repli sur TotalAmount.
        var firstMonth = new DateTime(Today.Year, Today.Month, 1).AddMonths(-5);
        var monthA = firstMonth.AddMonths(2);
        var monthB = firstMonth.AddMonths(3);
        await harness.SeedRunAsync(contract.Id, monthA.AddDays(-15), monthA,
            RecurringContractBillingRunStatus.Invoiced, fixedAmount: 100m);
        await harness.SeedRunAsync(contract.Id, monthB.AddDays(-15), monthB,
            RecurringContractBillingRunStatus.Invoiced, fixedAmount: 150m);
        await harness.SeedRunAsync(contract.Id, monthB.AddDays(-10), monthB,
            RecurringContractBillingRunStatus.Invoiced, fixedAmount: 50m);

        await using var sut = harness.CreateService();
        var points = await sut.GetEvolutionAsync(contract.Id, months: 6);

        Assert.NotNull(points);
        Assert.Equal(6, points!.Count);
        Assert.Equal(MonthKey(firstMonth), points[0].Month);
        Assert.Equal(MonthKey(Today), points[^1].Month);
        // Tri chronologique ascendant (le format yyyy-MM trie comme les dates).
        Assert.Equal(points.OrderBy(p => p.Month).Select(p => p.Month), points.Select(p => p.Month));

        var byMonth = points.ToDictionary(p => p.Month, p => p.Amount);
        Assert.Equal(100m, byMonth[MonthKey(monthA)]);
        Assert.Equal(200m, byMonth[MonthKey(monthB)]);   // deux runs du même mois agrégés
        Assert.Equal(0m, byMonth[MonthKey(firstMonth)]); // mois sans activité à 0
    }

    [Fact]
    public async Task Evolution_FillsMissingMonthsWithZero_AndReturnsExactlyMonthsPoints()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client série régulière");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        // Un seul run sur le mois courant.
        var currentMonth = new DateTime(Today.Year, Today.Month, 1);
        await harness.SeedRunAsync(contract.Id, currentMonth, currentMonth.AddDays(14),
            RecurringContractBillingRunStatus.Invoiced, fixedAmount: 75m);

        await using var sut = harness.CreateService();
        var points = await sut.GetEvolutionAsync(contract.Id, months: 6);

        Assert.NotNull(points);
        Assert.Equal(6, points!.Count);
        Assert.Equal(75m, points[^1].Amount);
        Assert.All(points.Take(5), p => Assert.Equal(0m, p.Amount));
    }

    [Fact]
    public async Task Evolution_ExcludesDraftCreatedAndFailedRuns()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client non facturé");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        var currentMonth = new DateTime(Today.Year, Today.Month, 1);
        await harness.SeedRunAsync(contract.Id, currentMonth, currentMonth.AddDays(14),
            RecurringContractBillingRunStatus.DraftCreated, fixedAmount: 100m);
        await harness.SeedRunAsync(contract.Id, currentMonth, currentMonth.AddDays(14),
            RecurringContractBillingRunStatus.Failed, fixedAmount: 200m);

        await using var sut = harness.CreateService();
        var points = await sut.GetEvolutionAsync(contract.Id, months: 6);

        Assert.NotNull(points);
        Assert.All(points!, p => Assert.Equal(0m, p.Amount));
    }

    [Fact]
    public async Task Evolution_MonthsClamped_1To36()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client clamp");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        await using var sut = harness.CreateService();

        var tooSmall = await sut.GetEvolutionAsync(contract.Id, months: 0);
        Assert.NotNull(tooSmall);
        Assert.Single(tooSmall!);
        Assert.Equal(MonthKey(Today), tooSmall![0].Month);

        var tooLarge = await sut.GetEvolutionAsync(contract.Id, months: 99);
        Assert.NotNull(tooLarge);
        Assert.Equal(36, tooLarge!.Count);
        Assert.Equal(MonthKey(Today), tooLarge[^1].Month);
    }

    [Fact]
    public async Task Evolution_UnknownContract_ReturnsNull()
    {
        var harness = new RecurringContractTestHarness();
        await using var sut = harness.CreateService();

        var points = await sut.GetEvolutionAsync(Guid.NewGuid(), months: 6);

        Assert.Null(points);
    }

    [Fact]
    public async Task Evolution_CreditNoteMakesNegativeMonth()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client avoir négatif");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        // Facture 100 HT sur le mois courant + avoir 150 HT rattaché → mois net à −50.
        var currentMonth = new DateTime(Today.Year, Today.Month, 1);
        var invoice = await harness.SeedInvoiceAsync(client.Id, currentMonth.AddDays(3), 100m);
        var run = await harness.SeedRunAsync(contract.Id, currentMonth, currentMonth.AddDays(14),
            RecurringContractBillingRunStatus.Invoiced, fixedAmount: 100m, invoiceId: invoice.Id);
        await harness.LinkInvoiceToContractAsync(invoice.Id, contract.Id, run.Id);
        await harness.SeedCreditNoteAsync(client.Id, invoice.Id, currentMonth.AddDays(5), 150m);

        await using var sut = harness.CreateService();
        var points = await sut.GetEvolutionAsync(contract.Id, months: 6);

        Assert.NotNull(points);
        var month = points!.Single(p => p.Month == MonthKey(Today));
        Assert.Equal(-50m, month.Amount);
    }
}
