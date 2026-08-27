using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

/// <summary>
/// Durcissement de la numérotation CTR-{année}-{seq:D4} (T2/D11).
/// Note : le provider InMemory ne valide pas l'index unique SQL — le chemin de retry sur
/// SqlException 2601/2627 est couvert par revue + test manuel sur SQL Server, pas ici.
/// </summary>
public sealed class RecurringContractNumberingTests
{
    private static UpsertRecurringContractDto NewDto(Guid clientId) => new()
    {
        ClientId = clientId,
        BillingFrequency = BillingFrequency.Monthly,
        BillingDayOfMonth = 1,
        StartDate = DateTime.UtcNow.Date,
        Lines = new[]
        {
            new UpsertRecurringContractLineDto
            {
                LineType = RecurringContractLineType.FixedRecurring,
                Description = "Abonnement",
                Quantity = 1,
                UnitPriceHT = 100m,
                VatRate = 19m
            }
        }
    };

    [Fact]
    public async Task CreateAsync_TwoContractsSameYear_GetSequentialDistinctNumbers()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client numérotation");
        await using var sut = harness.CreateService();

        var first = await sut.CreateAsync(NewDto(client.Id));
        var second = await sut.CreateAsync(NewDto(client.Id));

        Assert.True(first.IsSuccess, first.Error?.Description);
        Assert.True(second.IsSuccess, second.Error?.Description);

        await using var ctx = harness.Factory.CreateContext();
        var numbers = await ctx.RecurringContracts.Select(c => c.Number).ToListAsync();
        Assert.Equal(2, numbers.Count);
        Assert.Equal(2, numbers.Distinct().Count());
        var year = DateTime.UtcNow.Year;
        Assert.Contains($"CTR-{year}-0001", numbers);
        Assert.Contains($"CTR-{year}-0002", numbers);
    }

    [Fact]
    public async Task GenerateContractNumber_SkipsExistingCandidate()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client collision");
        var year = DateTime.UtcNow.Year;

        // Pré-seed un contrat portant le candidat calculé (comptage annuel = 1 → candidat 0002).
        await harness.SeedContractAsync(client.Id, number: $"CTR-{year}-0002");

        await using var sut = harness.CreateService();
        var created = await sut.CreateAsync(NewDto(client.Id));

        Assert.True(created.IsSuccess, created.Error?.Description);
        await using var ctx = harness.Factory.CreateContext();
        var contract = await ctx.RecurringContracts.FirstAsync(c => c.Id == created.Value);
        Assert.Equal($"CTR-{year}-0003", contract.Number);
    }
}
