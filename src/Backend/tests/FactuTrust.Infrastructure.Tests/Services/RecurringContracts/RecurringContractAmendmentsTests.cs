using FactuTrust.Domain.Entities.RecurringContracts;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

/// <summary>
/// Historique des avenants (T5) et traçabilité du cycle de vie suspend/resume (D17).
/// </summary>
public sealed class RecurringContractAmendmentsTests
{
    private static async Task<RecurringContractAmendment> SeedAmendmentAsync(
        RecurringContractTestHarness harness, Guid contractId,
        RecurringContractAmendmentType type, Guid? createdBy = null, string? notes = null)
    {
        var amendment = RecurringContractAmendment.Create(
            contractId, type, DateTime.UtcNow.Date, ProrationPolicy.DailyProration,
            notes, """[{"Id":"x"}]""", """[{"Id":"y"}]""", createdBy);
        await using var ctx = harness.Factory.CreateContext();
        ctx.RecurringContractAmendments.Add(amendment);
        await ctx.SaveChangesAsync();
        return amendment;
    }

    [Fact]
    public async Task ListAmendments_ReturnsDescendingByCreatedAt_WithDisplayLabels()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client avenants");
        var contract = await harness.SeedContractAsync(client.Id,
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));

        var first = await SeedAmendmentAsync(harness, contract.Id, RecurringContractAmendmentType.PriceChange);
        await Task.Delay(20); // garantit des CreatedAt distincts (granularité horloge)
        var second = await SeedAmendmentAsync(harness, contract.Id, RecurringContractAmendmentType.AddLine);

        await using var sut = harness.CreateService();
        var items = await sut.ListAmendmentsAsync(contract.Id);

        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);
        Assert.Equal(second.Id, items[0].Id); // plus récent en tête
        Assert.Equal(first.Id, items[1].Id);
        Assert.Equal("Ajout de ligne", items[0].TypeDisplay);
        Assert.Equal("Prorata journalier", items[0].ProrationPolicyDisplay);
        Assert.Equal("Changement de prix", items[1].TypeDisplay);
    }

    [Fact]
    public async Task ListAmendments_ResolvesCreatorName_WhenMemberFound()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client créateur connu");
        var contract = await harness.SeedContractAsync(client.Id,
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));
        var userId = Guid.NewGuid();
        harness.Members.Register(userId, "Amel Trabelsi");
        await SeedAmendmentAsync(harness, contract.Id, RecurringContractAmendmentType.Upgrade, createdBy: userId);

        await using var sut = harness.CreateService();
        var items = await sut.ListAmendmentsAsync(contract.Id);

        var dto = Assert.Single(items!);
        Assert.Equal(userId, dto.CreatedByUserId);
        Assert.Equal("Amel Trabelsi", dto.CreatedByUserName);
    }

    [Fact]
    public async Task ListAmendments_NullName_WhenMemberMissing()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client créateur inconnu");
        var contract = await harness.SeedContractAsync(client.Id,
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));
        await SeedAmendmentAsync(harness, contract.Id, RecurringContractAmendmentType.Upgrade, createdBy: Guid.NewGuid());

        await using var sut = harness.CreateService();
        var items = await sut.ListAmendmentsAsync(contract.Id);

        var dto = Assert.Single(items!);
        Assert.NotNull(dto.CreatedByUserId);
        Assert.Null(dto.CreatedByUserName); // best-effort : jamais d'échec pour un nom
    }

    [Fact]
    public async Task ListAmendments_UnknownContract_ReturnsNull()
    {
        var harness = new RecurringContractTestHarness();
        await using var sut = harness.CreateService();

        Assert.Null(await sut.ListAmendmentsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetAmendment_IncludesSnapshots()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client détail avenant");
        var contract = await harness.SeedContractAsync(client.Id,
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));
        var amendment = await SeedAmendmentAsync(harness, contract.Id, RecurringContractAmendmentType.Downgrade);

        await using var sut = harness.CreateService();
        var dto = await sut.GetAmendmentAsync(contract.Id, amendment.Id);

        Assert.NotNull(dto);
        Assert.Equal(amendment.Id, dto!.Id);
        Assert.Equal(contract.Id, dto.RecurringContractId);
        Assert.Equal("Réduction", dto.TypeDisplay);
        Assert.Equal("""[{"Id":"x"}]""", dto.SnapshotBeforeJson);
        Assert.Equal("""[{"Id":"y"}]""", dto.SnapshotAfterJson);
    }

    [Fact]
    public async Task GetAmendment_WrongContract_ReturnsNull()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client avenant autre contrat");
        var contract = await harness.SeedContractAsync(client.Id,
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));
        var other = await harness.SeedContractAsync(client.Id,
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));
        var amendment = await SeedAmendmentAsync(harness, contract.Id, RecurringContractAmendmentType.Upgrade);

        await using var sut = harness.CreateService();
        Assert.Null(await sut.GetAmendmentAsync(other.Id, amendment.Id));
    }

    [Fact]
    public async Task SuspendAsync_PersistsSuspendAmendment()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client suspension");
        var contract = await harness.SeedContractAsync(client.Id,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));

        await using var sut = harness.CreateService();
        var result = await sut.SuspendAsync(contract.Id);

        Assert.True(result.IsSuccess, result.Error?.Description);
        await using var ctx = harness.Factory.CreateContext();
        var amendment = await ctx.RecurringContractAmendments
            .SingleAsync(a => a.RecurringContractId == contract.Id);
        Assert.Equal(RecurringContractAmendmentType.Suspend, amendment.AmendmentType);
        Assert.Equal(ProrationPolicy.None, amendment.ProrationPolicy);
        Assert.Equal(harness.CurrentUser.UserId, amendment.CreatedByUserId);
        // JsonSerializer par défaut : enums sérialisés en int (Active=1, Suspended=2).
        Assert.Contains("\"Status\":1", amendment.SnapshotBeforeJson);
        Assert.Contains("\"Status\":2", amendment.SnapshotAfterJson);
    }

    [Fact]
    public async Task ResumeAsync_PersistsResumeAmendment()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client reprise");
        var contract = await harness.SeedContractAsync(client.Id,
            configure: c => { c.Activate(); c.Suspend(); },
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));

        await using var sut = harness.CreateService();
        var result = await sut.ResumeAsync(contract.Id);

        Assert.True(result.IsSuccess, result.Error?.Description);
        await using var ctx = harness.Factory.CreateContext();
        var amendment = await ctx.RecurringContractAmendments
            .SingleAsync(a => a.RecurringContractId == contract.Id);
        Assert.Equal(RecurringContractAmendmentType.Resume, amendment.AmendmentType);
        Assert.Contains("\"Status\":2", amendment.SnapshotBeforeJson);
        Assert.Contains("\"Status\":1", amendment.SnapshotAfterJson);
    }
}
