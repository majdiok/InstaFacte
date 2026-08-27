using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

/// <summary>Renouvellement côté service (T4) : persistance de l'avenant Renewal (D7).</summary>
public sealed class RecurringContractRenewServiceTests
{
    [Fact]
    public async Task RenewAsync_PersistsRenewalAmendment_WithBeforeAfterSnapshots()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client renew");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), endDate: new DateTime(2026, 12, 31),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        await using var sut = harness.CreateService();
        var result = await sut.RenewAsync(contract.Id,
            new RenewRecurringContractDto { Notes = "Renouvellement anticipé" });

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(new DateTime(2027, 12, 31), result.Value);

        await using var ctx = harness.Factory.CreateContext();
        var amendment = await ctx.RecurringContractAmendments
            .SingleAsync(a => a.RecurringContractId == contract.Id);
        Assert.Equal(RecurringContractAmendmentType.Renewal, amendment.AmendmentType);
        Assert.Equal(ProrationPolicy.None, amendment.ProrationPolicy);
        Assert.Equal("Renouvellement anticipé", amendment.Notes);
        Assert.Equal(harness.CurrentUser.UserId, amendment.CreatedByUserId);
        Assert.NotNull(amendment.SnapshotBeforeJson);
        Assert.NotNull(amendment.SnapshotAfterJson);
        Assert.Contains("2026-12-31", amendment.SnapshotBeforeJson);
        Assert.Contains("2027-12-31", amendment.SnapshotAfterJson);

        var reloaded = await ctx.RecurringContracts.FirstAsync(c => c.Id == contract.Id);
        Assert.Equal(new DateTime(2027, 12, 31), reloaded.EndDate);
        Assert.Equal(RecurringContractStatus.Active, reloaded.Status);
    }

    [Fact]
    public async Task RenewAsync_UnknownContract_NotFound()
    {
        var harness = new RecurringContractTestHarness();
        await using var sut = harness.CreateService();

        var result = await sut.RenewAsync(Guid.NewGuid(), new RenewRecurringContractDto());

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }
}
