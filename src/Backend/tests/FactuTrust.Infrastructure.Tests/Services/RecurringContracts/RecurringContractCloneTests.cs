using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities.RecurringContracts;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

/// <summary>Clone d'un contrat en brouillon (T3/D6).</summary>
public sealed class RecurringContractCloneTests
{
    [Fact]
    public async Task Clone_CopiesActiveLinesOnly_AndResetsLineEffectivity()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client clone");
        var startDate = new DateTime(2026, 1, 1);
        var source = await harness.SeedContractAsync(client.Id, startDate: startDate,
            configure: c => c.Activate(),
            lines: c =>
            {
                c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId);
                c.AddLine(RecurringContractLineType.FixedRecurring, "Ancienne option", 2, 40m, 19m, RecurringContractTestHarness.DefaultProductId);
            });

        // Désactive la seconde ligne directement sur l'entité seedée, puis re-persiste.
        await using (var ctx = harness.Factory.CreateContext())
        {
            var tracked = await ctx.RecurringContracts.Include(c => c.Lines)
                .FirstAsync(c => c.Id == source.Id);
            tracked.Lines.First(l => l.Description == "Ancienne option")
                .Deactivate(new DateTime(2026, 6, 30));
            await ctx.SaveChangesAsync();
        }

        await using var sut = harness.CreateService();
        var cloneDate = new DateTime(2026, 9, 1);
        var result = await sut.CloneAsync(source.Id, new CloneRecurringContractDto { StartDate = cloneDate });

        Assert.True(result.IsSuccess, result.Error?.Description);
        await using var read = harness.Factory.CreateContext();
        var clone = await read.RecurringContracts.Include(c => c.Lines)
            .FirstAsync(c => c.Id == result.Value);

        Assert.Equal(RecurringContractStatus.Draft, clone.Status);
        var line = Assert.Single(clone.Lines);
        Assert.Equal("Abonnement", line.Description);
        Assert.Equal(cloneDate, line.EffectiveFrom);  // fenêtre réinitialisée
        Assert.Null(line.EffectiveTo);
        Assert.Equal(cloneDate, clone.StartDate);
    }

    [Fact]
    public async Task Clone_DefaultsStartDateToToday_AndShiftsEndDateByOriginalDuration()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client clone dates");
        var source = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), endDate: new DateTime(2026, 12, 31),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));

        await using var sut = harness.CreateService();
        var result = await sut.CloneAsync(source.Id, new CloneRecurringContractDto());

        Assert.True(result.IsSuccess, result.Error?.Description);
        await using var ctx = harness.Factory.CreateContext();
        var clone = await ctx.RecurringContracts.FirstAsync(c => c.Id == result.Value);

        var today = DateTime.UtcNow.Date;
        Assert.Equal(today, clone.StartDate);
        // Durée d'origine : 31/12/2026 − 01/01/2026 = 364 jours.
        Assert.Equal(today.AddDays(364), clone.EndDate);
    }

    [Fact]
    public async Task Clone_WithoutEndDate_CloneHasNullEndDate()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client clone sans fin");
        var source = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));

        await using var sut = harness.CreateService();
        var result = await sut.CloneAsync(source.Id, new CloneRecurringContractDto());

        Assert.True(result.IsSuccess, result.Error?.Description);
        await using var ctx = harness.Factory.CreateContext();
        var clone = await ctx.RecurringContracts.FirstAsync(c => c.Id == result.Value);
        Assert.Null(clone.EndDate);
    }

    [Fact]
    public async Task Clone_DoesNotCopyRunsAmendmentsOrSourceQuote()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client clone historique");
        var quoteId = Guid.NewGuid();
        var source = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), sourceQuoteId: quoteId,
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));
        await harness.SeedRunAsync(source.Id, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31),
            RecurringContractBillingRunStatus.DraftCreated, fixedAmount: 100m);

        await using (var ctx = harness.Factory.CreateContext())
        {
            ctx.RecurringContractAmendments.Add(RecurringContractAmendment.Create(
                source.Id, RecurringContractAmendmentType.PriceChange, new DateTime(2026, 2, 1),
                ProrationPolicy.DailyProration, null, "[]", "[]", Guid.NewGuid()));
            await ctx.SaveChangesAsync();
        }

        await using var sut = harness.CreateService();
        var result = await sut.CloneAsync(source.Id, new CloneRecurringContractDto());

        Assert.True(result.IsSuccess, result.Error?.Description);
        await using var read = harness.Factory.CreateContext();
        var clone = await read.RecurringContracts.FirstAsync(c => c.Id == result.Value);
        Assert.Null(clone.SourceQuoteId);
        Assert.False(clone.SetupFeeBilled);
        Assert.False(await read.RecurringContractBillingRuns.AnyAsync(r => r.RecurringContractId == clone.Id));
        Assert.False(await read.RecurringContractAmendments.AnyAsync(a => a.RecurringContractId == clone.Id));
    }

    [Fact]
    public async Task Clone_AssignsNewUniqueNumber()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client clone numéro");
        var source = await harness.SeedContractAsync(client.Id,
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));

        await using var sut = harness.CreateService();
        var result = await sut.CloneAsync(source.Id, new CloneRecurringContractDto());

        Assert.True(result.IsSuccess, result.Error?.Description);
        await using var ctx = harness.Factory.CreateContext();
        var clone = await ctx.RecurringContracts.FirstAsync(c => c.Id == result.Value);
        Assert.NotNull(clone.Number);
        Assert.StartsWith("CTR-", clone.Number);
        Assert.NotEqual(source.Number, clone.Number);
    }

    [Fact]
    public async Task Clone_UnknownSource_ReturnsNotFound()
    {
        var harness = new RecurringContractTestHarness();
        await using var sut = harness.CreateService();

        var result = await sut.CloneAsync(Guid.NewGuid(), new CloneRecurringContractDto());

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Clone_UnknownClientOverride_ReturnsValidationError()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client clone client invalide");
        var source = await harness.SeedContractAsync(client.Id,
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));

        await using var sut = harness.CreateService();
        var result = await sut.CloneAsync(source.Id,
            new CloneRecurringContractDto { ClientId = Guid.NewGuid() });

        Assert.True(result.IsFailure);
        Assert.Contains("Client", result.Error.Description);
    }
}
