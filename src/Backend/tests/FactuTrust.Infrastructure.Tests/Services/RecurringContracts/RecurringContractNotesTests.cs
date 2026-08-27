using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

/// <summary>Édition des notes sur tout statut (T13/D15) — le PUT complet reste réservé au brouillon.</summary>
public sealed class RecurringContractNotesTests
{
    [Fact]
    public async Task UpdateNotes_ActiveContract_Succeeds()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client notes");
        // Verrouille D15 : le contrat est ACTIF (le PUT complet serait refusé sur ce statut).
        var contract = await harness.SeedContractAsync(client.Id,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        await using var sut = harness.CreateService();
        var result = await sut.UpdateNotesAsync(contract.Id, "Note mise à jour sur contrat actif");

        Assert.True(result.IsSuccess, result.Error?.Description);
        await using var ctx = harness.Factory.CreateContext();
        var reloaded = await ctx.RecurringContracts.FirstAsync(c => c.Id == contract.Id);
        Assert.Equal("Note mise à jour sur contrat actif", reloaded.Notes);
        Assert.Equal(RecurringContractStatus.Active, reloaded.Status);
    }

    [Fact]
    public async Task UpdateNotes_SetsNullOnEmpty()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client notes vides");
        var contract = await harness.SeedContractAsync(client.Id, notes: "Note initiale",
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        await using var sut = harness.CreateService();
        var result = await sut.UpdateNotesAsync(contract.Id, "   ");

        Assert.True(result.IsSuccess, result.Error?.Description);
        await using var ctx = harness.Factory.CreateContext();
        var reloaded = await ctx.RecurringContracts.FirstAsync(c => c.Id == contract.Id);
        Assert.Null(reloaded.Notes);
    }

    [Fact]
    public async Task UpdateNotes_UnknownContract_NotFound()
    {
        var harness = new RecurringContractTestHarness();
        await using var sut = harness.CreateService();

        var result = await sut.UpdateNotesAsync(Guid.NewGuid(), "Note");

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }
}
