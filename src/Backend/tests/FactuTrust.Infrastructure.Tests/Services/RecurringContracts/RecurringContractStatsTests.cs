using FactuTrust.Domain.Entities.RecurringContracts;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

/// <summary>KPI de la page liste (T14/D16) : compteurs actifs, MRR normalisé, échéances, brouillons.</summary>
public sealed class RecurringContractStatsTests
{
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    [Fact]
    public async Task Stats_CountsActiveAndComputesMrrAcrossFrequencies()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client stats");
        // Mensuel 100 → 100 ; trimestriel 300 → 100 ; annuel 1200 → 100. MRR attendu : 300.
        await harness.SeedContractAsync(client.Id, frequency: BillingFrequency.Monthly,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Mensuel", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));
        await harness.SeedContractAsync(client.Id, frequency: BillingFrequency.Quarterly,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Trimestriel", 1, 300m, 19m, RecurringContractTestHarness.DefaultProductId));
        await harness.SeedContractAsync(client.Id, frequency: BillingFrequency.Annual,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Annuel", 1, 1200m, 19m, RecurringContractTestHarness.DefaultProductId));
        // Un brouillon ne compte ni dans ActiveCount ni dans le MRR.
        await harness.SeedContractAsync(client.Id, frequency: BillingFrequency.Monthly,
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Brouillon", 1, 999m, 19m, RecurringContractTestHarness.DefaultProductId));

        await using var sut = harness.CreateService();
        var stats = await sut.GetStatsAsync();

        Assert.Equal(3, stats.ActiveCount);
        Assert.Equal(300m, stats.EstimatedMonthlyRecurringTotal);
        Assert.Equal("TND", stats.Currency);
    }

    [Fact]
    public async Task Stats_DueSoon_WithinSevenDaysOnly()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client échéances");
        var dueSoon = await harness.SeedContractAsync(client.Id,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Proche", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));
        var dueLater = await harness.SeedContractAsync(client.Id,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Lointaine", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));
        // Contrat brouillon : jamais compté (pas Active).
        await harness.SeedContractAsync(client.Id,
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Brouillon", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));

        // NextBillingDate fixée de façon déterministe via l'entrée EF (private set côté domaine).
        await using (var ctx = harness.Factory.CreateContext())
        {
            var soon = await ctx.RecurringContracts.FirstAsync(c => c.Id == dueSoon.Id);
            ctx.Entry(soon).Property(nameof(RecurringContract.NextBillingDate)).CurrentValue = Today.AddDays(5);
            var later = await ctx.RecurringContracts.FirstAsync(c => c.Id == dueLater.Id);
            ctx.Entry(later).Property(nameof(RecurringContract.NextBillingDate)).CurrentValue = Today.AddDays(10);
            await ctx.SaveChangesAsync();
        }

        await using var sut = harness.CreateService();
        var stats = await sut.GetStatsAsync();

        Assert.Equal(2, stats.ActiveCount);
        Assert.Equal(1, stats.DueSoonCount); // J+5 compté, J+10 non
    }

    [Fact]
    public async Task Stats_PendingDrafts_DraftCreatedWithDraftIdOnly()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client brouillons");
        var contract = await harness.SeedContractAsync(client.Id,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));

        // Seul le run DraftCreated (avec brouillon associé) compte.
        await harness.SeedRunAsync(contract.Id, new DateTime(2026, 7, 1), new DateTime(2026, 7, 31),
            RecurringContractBillingRunStatus.DraftCreated, fixedAmount: 100m);
        await harness.SeedRunAsync(contract.Id, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30),
            RecurringContractBillingRunStatus.Invoiced, fixedAmount: 100m);
        await harness.SeedRunAsync(contract.Id, new DateTime(2026, 8, 1), new DateTime(2026, 8, 31),
            RecurringContractBillingRunStatus.Pending);

        await using var sut = harness.CreateService();
        var stats = await sut.GetStatsAsync();

        Assert.Equal(1, stats.PendingDraftsCount);
    }

    [Fact]
    public async Task Stats_NoContracts_ReturnsZeros()
    {
        var harness = new RecurringContractTestHarness();
        await using var sut = harness.CreateService();

        var stats = await sut.GetStatsAsync();

        Assert.Equal(0, stats.ActiveCount);
        Assert.Equal(0m, stats.EstimatedMonthlyRecurringTotal);
        Assert.Equal(0, stats.DueSoonCount);
        Assert.Equal(0, stats.PendingDraftsCount);
        Assert.Equal("TND", stats.Currency);
    }
}
