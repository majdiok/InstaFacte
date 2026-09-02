using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

public sealed class RecurringContractCreateValidationTests
{
    [Fact]
    public async Task CreateAsync_LineWithoutProduct_ReturnsValidationFailure()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client sans produit");
        await using var sut = harness.CreateService();

        var result = await sut.CreateAsync(new UpsertRecurringContractDto
        {
            ClientId = client.Id,
            BillingFrequency = BillingFrequency.Monthly,
            BillingDayOfMonth = 1,
            StartDate = DateTime.UtcNow.Date,
            Lines =
            [
                new UpsertRecurringContractLineDto
                {
                    LineType = RecurringContractLineType.FixedRecurring,
                    Description = "Abonnement",
                    Quantity = 1,
                    UnitPriceHT = 100m,
                    VatRate = 19m
                }
            ]
        });

        Assert.True(result.IsFailure);
        Assert.Contains("produit", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_OneTimeSetupWithoutProduct_Succeeds()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client setup");
        await using var sut = harness.CreateService();

        var result = await sut.CreateAsync(new UpsertRecurringContractDto
        {
            ClientId = client.Id,
            BillingFrequency = BillingFrequency.Monthly,
            BillingDayOfMonth = 1,
            StartDate = DateTime.UtcNow.Date,
            Lines =
            [
                new UpsertRecurringContractLineDto
                {
                    LineType = RecurringContractLineType.OneTimeSetup,
                    Description = "Installation",
                    Quantity = 1,
                    UnitPriceHT = 500m,
                    VatRate = 19m
                }
            ]
        });

        Assert.True(result.IsSuccess, result.Error?.Description);
    }
}
