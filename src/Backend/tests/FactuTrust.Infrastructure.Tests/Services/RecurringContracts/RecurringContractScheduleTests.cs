using FactuTrust.Domain.Entities.RecurringContracts;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

/// <summary>Échéancier prévisionnel (T6) : fusion runs/projection, statuts D3, montants D2.</summary>
public sealed class RecurringContractScheduleTests
{
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    [Fact]
    public async Task GetSchedule_ActiveMonthlyContract_ReturnsCountProjectedOccurrences()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client schedule");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: Today.AddMonths(1), // démarrage futur → aucune occurrence en retard
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        await using var sut = harness.CreateService();
        var items = await sut.GetScheduleAsync(contract.Id, count: 12);

        Assert.NotNull(items);
        Assert.Equal(12, items!.Count);
        Assert.All(items, i => Assert.Equal(RecurringContractScheduleOccurrenceStatus.Upcoming, i.Status));
        Assert.All(items, i => Assert.Equal("À venir", i.StatusDisplay));
        Assert.All(items, i => Assert.Equal(100m, i.EstimatedAmountHT));
        Assert.All(items, i => Assert.Null(i.BillingRunId));
    }

    [Fact]
    public async Task GetSchedule_MergesExistingRuns_ByPeriodKey()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client fusion");
        var periodFrom = new DateTime(Today.Year, Today.Month, 1);
        var periodTo = periodFrom.AddMonths(1).AddDays(-1);
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: periodFrom,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        var invoiceId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var run = await harness.SeedRunAsync(contract.Id, periodFrom, periodTo,
            RecurringContractBillingRunStatus.Invoiced, fixedAmount: 100m,
            invoiceDraftId: draftId, invoiceId: invoiceId);

        await using var sut = harness.CreateService();
        var items = await sut.GetScheduleAsync(contract.Id, count: 12);

        Assert.NotNull(items);
        Assert.Equal(12, items!.Count); // le run consomme la première occurrence, pas de doublon
        Assert.Single(items, i => i.PeriodFrom == periodFrom);
        var merged = items.Single(i => i.PeriodFrom == periodFrom);
        Assert.Equal(RecurringContractScheduleOccurrenceStatus.Invoiced, merged.Status);
        Assert.Equal("Facturée", merged.StatusDisplay);
        Assert.Equal(run.Id, merged.BillingRunId);
        Assert.Equal(draftId, merged.InvoiceDraftId);
        Assert.Equal(invoiceId, merged.InvoiceId);
    }

    [Fact]
    public async Task GetSchedule_FailedRunOnCurrentPeriod_MarksFirstOccurrenceFailed()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client échec");
        var periodFrom = new DateTime(Today.Year, Today.Month, 1);
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: periodFrom,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        var run = await harness.SeedRunAsync(contract.Id, periodFrom, periodFrom.AddMonths(1).AddDays(-1),
            RecurringContractBillingRunStatus.Failed);

        await using var sut = harness.CreateService();
        var items = await sut.GetScheduleAsync(contract.Id, count: 3);

        var failed = items!.Single(i => i.PeriodFrom == periodFrom);
        Assert.Equal(RecurringContractScheduleOccurrenceStatus.Failed, failed.Status);
        Assert.Equal("Échouée", failed.StatusDisplay);
        Assert.Equal(run.Id, failed.BillingRunId);
    }

    [Fact]
    public async Task GetSchedule_OverdueOccurrence_WhenActiveAndDatePassed()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client retard");
        // Début 2 mois avant le mois courant → première occurrence projetée déjà passée.
        var start = new DateTime(Today.Year, Today.Month, 1).AddMonths(-2);
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: start,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        await using var sut = harness.CreateService();
        var items = await sut.GetScheduleAsync(contract.Id, count: 3);

        Assert.NotNull(items);
        var overdue = items!.Where(i => i.Date < Today).ToList();
        Assert.NotEmpty(overdue);
        Assert.All(overdue, i => Assert.Equal(RecurringContractScheduleOccurrenceStatus.Overdue, i.Status));
        Assert.All(overdue, i => Assert.Equal("En retard", i.StatusDisplay));
    }

    [Fact]
    public async Task GetSchedule_CancelledContract_ReturnsOnlyExistingRuns()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client résilié");
        var start = new DateTime(Today.Year, Today.Month, 1).AddMonths(-2);
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: start,
            configure: c => { c.Activate(); c.Cancel(); },
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        var run = await harness.SeedRunAsync(contract.Id, start, start.AddMonths(1).AddDays(-1),
            RecurringContractBillingRunStatus.Invoiced, fixedAmount: 100m);

        await using var sut = harness.CreateService();
        var items = await sut.GetScheduleAsync(contract.Id, count: 12);

        var item = Assert.Single(items!); // contrat clos : aucune projection, seuls les runs
        Assert.Equal(run.Id, item.BillingRunId);
        Assert.Equal(RecurringContractScheduleOccurrenceStatus.Invoiced, item.Status);
        Assert.Equal(100m, item.EstimatedAmountHT);  // montant réel du run
        Assert.Equal(0m, item.EstimatedAmountTTC);   // TTC « estimé » non calculé pour un run passé
    }

    [Fact]
    public async Task GetSchedule_DraftContract_ProjectsFromInitialDate_AllUpcoming()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client brouillon");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: Today.AddMonths(1),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        await using var sut = harness.CreateService();
        var items = await sut.GetScheduleAsync(contract.Id, count: 6);

        Assert.Equal(6, items!.Count);
        Assert.All(items, i => Assert.Equal(RecurringContractScheduleOccurrenceStatus.Upcoming, i.Status));
        // NextBillingDate null (brouillon) → départ de la date initiale calculée
        // (jour 1 déjà passé à la date de début → premier du mois suivant).
        var expectedInitial = FactuTrust.Domain.Services.RecurringContractScheduleProjector
            .ComputeInitialBillingDate(Today.AddMonths(1), 1);
        Assert.Equal(expectedInitial, items.Min(i => i.Date));
    }

    [Fact]
    public async Task GetSchedule_EstimatedAmount_MatchesProrataRules()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client prorata");
        var periodFrom = new DateTime(Today.Year, Today.Month, 1);
        var periodTo = periodFrom.AddMonths(1).AddDays(-1);
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: periodFrom,
            configure: c =>
            {
                c.Activate();
                // Avenant du jour : ajoute une ligne 3100 effective aujourd'hui (fenêtre d'effet).
                var existing = c.Lines.Single();
                var newLine = RecurringContractLine.Create(
                    c.Id, RecurringContractLineType.FixedRecurring, "Renfort", 1, 3100m, 19m, Today).Value;
                Assert.True(c.AmendLines(Today, new[]
                {
                    new RecurringContractAmendLineTarget(existing.Id, RecurringContractLine.Create(
                        c.Id, RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, Today).Value),
                    new RecurringContractAmendLineTarget(null, newLine)
                }, Today).IsSuccess);
            },
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        await using var sut = harness.CreateService();
        var items = await sut.GetScheduleAsync(contract.Id, count: 3);

        // Période courante : 100 + 3100 × (jours restants / jours du mois).
        var remainingDays = (periodTo - Today).Days + 1;
        var totalDays = (periodTo - periodFrom).Days + 1;
        var expectedProrata = decimal.Round(3100m * remainingDays / totalDays, 3, MidpointRounding.AwayFromZero);
        var current = items!.Single(i => i.PeriodFrom == periodFrom);
        Assert.Equal(decimal.Round(100m + expectedProrata, 3), current.EstimatedAmountHT);

        // Période suivante : plein tarif des deux lignes.
        var next = items!.Single(i => i.PeriodFrom == periodFrom.AddMonths(1));
        Assert.Equal(3200m, next.EstimatedAmountHT);
        Assert.Equal(3200m * 1.19m, next.EstimatedAmountTTC);
    }

    [Fact]
    public async Task GetSchedule_OneTimeSetup_OnlyOnFirstOccurrence_WhenNotBilled()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client setup");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: Today.AddMonths(1),
            lines: c =>
            {
                c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m);
                c.AddLine(RecurringContractLineType.OneTimeSetup, "Installation", 1, 500m, 19m);
            });

        await using var sut = harness.CreateService();
        var items = await sut.GetScheduleAsync(contract.Id, count: 3);

        var ordered = items!.OrderBy(i => i.Date).ToList();
        Assert.Equal(600m, ordered[0].EstimatedAmountHT);  // setup sur la 1ʳᵉ occurrence
        Assert.Equal(100m, ordered[1].EstimatedAmountHT);
        Assert.Equal(100m, ordered[2].EstimatedAmountHT);
    }

    [Fact]
    public async Task GetSchedule_UsageEstimate_AveragesLast3InvoicedRuns()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client usage");
        var start = new DateTime(Today.Year, Today.Month, 1);
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: start,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.UsageMetered, "Consommation", 1, 1m, 19m,
                usageMetricId: Guid.NewGuid()));

        // 3 runs Invoiced sur les périodes précédentes : usage 10, 20, 60 → moyenne 30.
        var usageAmounts = new[] { 10m, 20m, 60m };
        for (var i = 3; i >= 1; i--)
        {
            var from = start.AddMonths(-i);
            await harness.SeedRunAsync(contract.Id, from, from.AddMonths(1).AddDays(-1),
                RecurringContractBillingRunStatus.Invoiced, usageAmount: usageAmounts[3 - i]);
        }

        await using var sut = harness.CreateService();
        var items = await sut.GetScheduleAsync(contract.Id, count: 3);

        var projected = items!.Where(i => i.BillingRunId is null).ToList();
        Assert.NotEmpty(projected);
        Assert.All(projected, i => Assert.Equal(30m, i.EstimatedAmountHT));
        Assert.All(projected, i => Assert.Equal(35.7m, i.EstimatedAmountTTC)); // 30 × 1,19
    }

    [Fact]
    public async Task GetSchedule_UnknownContract_ReturnsNull()
    {
        var harness = new RecurringContractTestHarness();
        await using var sut = harness.CreateService();

        Assert.Null(await sut.GetScheduleAsync(Guid.NewGuid(), 12));
    }

    [Fact]
    public async Task GetSchedule_CountIsClamped()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client clamp");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: Today.AddMonths(1),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        await using var sut = harness.CreateService();

        var one = await sut.GetScheduleAsync(contract.Id, count: 0);
        Assert.Single(one!); // clampé à 1

        var many = await sut.GetScheduleAsync(contract.Id, count: 500);
        Assert.Equal(60, many!.Count); // clampé à 60
    }
}
