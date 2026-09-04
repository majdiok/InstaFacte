using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

/// <summary>Avenants de lignes sur contrat actif (T12/D14) : fenêtres d'effet à effet immédiat.</summary>
public sealed class RecurringContractAmendActiveTests
{
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    private static AmendRecurringContractDto PriceChangeDto(Guid? sourceLineId, string description,
        decimal quantity, decimal unitPriceHt, DateTime effectiveDate) => new()
    {
        AmendmentType = RecurringContractAmendmentType.PriceChange,
        EffectiveDate = effectiveDate,
        UpdatedContract = new UpsertRecurringContractDto
        {
            ClientId = Guid.NewGuid(), // ignoré sur le chemin Active (seules les lignes sont lues)
            BillingFrequency = BillingFrequency.Monthly,
            StartDate = effectiveDate,
            Lines =
            [
                new UpsertRecurringContractLineDto
                {
                    Id = sourceLineId,
                    LineType = RecurringContractLineType.FixedRecurring,
                    Description = description,
                    Quantity = quantity,
                    UnitPriceHT = unitPriceHt,
                    VatRate = 19m,
                    ProductId = RecurringContractTestHarness.DefaultProductId,
                    SortOrder = 0
                }
            ]
        }
    };

    [Fact]
    public async Task Amend_Active_UsesEffectivityWindows_AndPersistsSnapshotsWithDates()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client avenant actif");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));
        var sourceLineId = contract.Lines.Single().Id;

        await using var sut = harness.CreateService();
        var result = await sut.AmendAsync(contract.Id,
            PriceChangeDto(sourceLineId, "Abonnement", 1, 120m, Today));

        Assert.True(result.IsSuccess, result.Error?.Description);

        await using var ctx = harness.Factory.CreateContext();
        var reloaded = await ctx.RecurringContracts.Include(c => c.Lines)
            .FirstAsync(c => c.Id == contract.Id);

        // Ancienne ligne clôturée à J−1, nouvelle ligne dès J au nouveau prix.
        var oldLine = reloaded.Lines.Single(l => l.Id == sourceLineId);
        Assert.False(oldLine.IsActive);
        Assert.Equal(Today.AddDays(-1), oldLine.EffectiveTo);

        var newLine = reloaded.Lines.Single(l => l.Id != sourceLineId);
        Assert.True(newLine.IsActive);
        Assert.Equal(Today, newLine.EffectiveFrom);
        Assert.Null(newLine.EffectiveTo);
        Assert.Equal(120m, newLine.UnitPriceHT);

        // L'avenant est tracé avec des snapshots avant/après incluant les fenêtres d'effet.
        var amendment = await ctx.RecurringContractAmendments
            .SingleAsync(a => a.RecurringContractId == contract.Id);
        Assert.Equal(RecurringContractAmendmentType.PriceChange, amendment.AmendmentType);
        Assert.Contains("EffectiveFrom", amendment.SnapshotBeforeJson);
        Assert.Contains("EffectiveTo", amendment.SnapshotBeforeJson);
        Assert.Contains("EffectiveFrom", amendment.SnapshotAfterJson);
        Assert.Contains("EffectiveTo", amendment.SnapshotAfterJson);
    }

    [Fact]
    public async Task Amend_Active_WithoutUpdatedContract_Fails()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client avenant sans cible");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));

        await using var sut = harness.CreateService();
        var result = await sut.AmendAsync(contract.Id, new AmendRecurringContractDto
        {
            AmendmentType = RecurringContractAmendmentType.PriceChange,
            EffectiveDate = Today,
            UpdatedContract = null
        });

        Assert.True(result.IsFailure);
        Assert.Contains("lignes cibles", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Amend_Active_NextBillingDateUnchanged()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client avenant échéance");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));
        var sourceLineId = contract.Lines.Single().Id;
        var nextBillingBefore = contract.NextBillingDate;

        await using var sut = harness.CreateService();
        var result = await sut.AmendAsync(contract.Id,
            PriceChangeDto(sourceLineId, "Abonnement", 1, 120m, Today));

        Assert.True(result.IsSuccess, result.Error?.Description);
        await using var ctx = harness.Factory.CreateContext();
        var reloaded = await ctx.RecurringContracts.FirstAsync(c => c.Id == contract.Id);
        Assert.Equal(nextBillingBefore, reloaded.NextBillingDate);
    }

    [Fact]
    public async Task Amend_Draft_KeepsReplaceAllBehavior()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client avenant brouillon");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1),
            lines: c =>
            {
                c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId);
                c.AddLine(RecurringContractLineType.OneTimeSetup, "Installation", 1, 50m, 19m);
            });

        await using var sut = harness.CreateService();
        var result = await sut.AmendAsync(contract.Id, new AmendRecurringContractDto
        {
            AmendmentType = RecurringContractAmendmentType.Upgrade,
            EffectiveDate = Today,
            UpdatedContract = new UpsertRecurringContractDto
            {
                ClientId = client.Id,
                BillingFrequency = BillingFrequency.Monthly,
                StartDate = new DateTime(2026, 1, 1),
                Lines =
                [
                    new UpsertRecurringContractLineDto
                    {
                        LineType = RecurringContractLineType.FixedRecurring,
                        Description = "Abonnement premium",
                        Quantity = 1,
                        UnitPriceHT = 200m,
                        VatRate = 19m,
                        ProductId = RecurringContractTestHarness.DefaultProductId,
                        SortOrder = 0
                    }
                ]
            }
        });

        Assert.True(result.IsSuccess, result.Error?.Description);
        await using var ctx = harness.Factory.CreateContext();
        var reloaded = await ctx.RecurringContracts.Include(c => c.Lines)
            .FirstAsync(c => c.Id == contract.Id);
        // Chemin brouillon inchangé : remplacement complet des lignes (replace-all).
        var line = Assert.Single(reloaded.Lines);
        Assert.Equal("Abonnement premium", line.Description);
        Assert.Equal(200m, line.UnitPriceHT);
        Assert.Equal(RecurringContractStatus.Draft, reloaded.Status);
    }

    [Fact]
    public async Task Amend_Suspended_FailsWithClearError()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client avenant suspendu");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1),
            configure: c =>
            {
                c.Activate();
                c.Suspend();
            },
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));
        var sourceLineId = contract.Lines.Single().Id;

        await using var sut = harness.CreateService();
        var result = await sut.AmendAsync(contract.Id,
            PriceChangeDto(sourceLineId, "Abonnement", 1, 120m, Today));

        Assert.True(result.IsFailure);
        Assert.Contains("suspendu", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Amend_UnknownContract_NotFound()
    {
        var harness = new RecurringContractTestHarness();
        await using var sut = harness.CreateService();

        var result = await sut.AmendAsync(Guid.NewGuid(),
            PriceChangeDto(null, "Abonnement", 1, 120m, Today));

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }
}
