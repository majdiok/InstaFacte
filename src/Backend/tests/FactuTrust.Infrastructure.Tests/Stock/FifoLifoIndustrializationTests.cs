using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Stock.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Stock;

public sealed class FifoLifoIndustrializationTests
{
    [Fact]
    public void FeaturesStock_BindsFifoLifoValuationEnabled()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:Stock:FifoLifoValuationEnabled"] = "true",
                ["Features:Stock:LotTrackingEnabled"] = "true"
            })
            .Build();

        var options = new StockTraceabilityOptions();
        config.GetSection(StockTraceabilityOptions.SectionName).Bind(options);

        Assert.True(options.FifoLifoValuationEnabled);
        Assert.True(options.LotTrackingEnabled);
    }

    [Fact]
    public async Task ApplyExitsAsync_WhenAnyLineLiveTracked_IncludeUntrackedAppliesCmupLine()
    {
        var fifo = CreateStockProduct("FIFO-1");
        Assert.True(fifo.ConfigureTraceability(
            TrackingMode.None, false, PickingPolicy.None, CostingMethod.Fifo, null).IsSuccess);
        var average = CreateStockProduct("CMUP-1");

        var products = new Mock<IProductRepository>();
        products.Setup(p => p.GetByIdAsync(fifo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(fifo);
        products.Setup(p => p.GetByIdAsync(average.Id, It.IsAny<CancellationToken>())).ReturnsAsync(average);

        var mutation = new RecordingStockMutationService();
        var sut = new TrackedDocumentStockService(
            products.Object,
            mutation,
            Options.Create(new StockTraceabilityOptions { FifoLifoValuationEnabled = true }));

        var warehouseId = Guid.NewGuid();
        var mixed = await sut.ApplyExitsAsync(
            warehouseId,
            "Facture MIX-1",
            MovementReason.Sale,
            new[]
            {
                new TrackedDocumentLine(fifo.Id, 2m, Guid.NewGuid(), StockDocumentKind.Invoice),
                new TrackedDocumentLine(average.Id, 3m, Guid.NewGuid(), StockDocumentKind.Invoice)
            },
            CancellationToken.None,
            includeUntracked: true);

        Assert.True(mixed.IsSuccess, mixed.Error?.Description);
        Assert.Equal(2, mutation.Requests.Count);
        Assert.Contains(mutation.Requests, r => r.ProductId == fifo.Id && r.Quantity == 2m);
        Assert.Contains(mutation.Requests, r => r.ProductId == average.Id && r.Quantity == 3m);

        mutation.Requests.Clear();
        var trackedOnly = await sut.ApplyExitsAsync(
            warehouseId,
            "Facture MIX-2",
            MovementReason.Sale,
            new[]
            {
                new TrackedDocumentLine(fifo.Id, 2m, Guid.NewGuid(), StockDocumentKind.Invoice),
                new TrackedDocumentLine(average.Id, 3m, Guid.NewGuid(), StockDocumentKind.Invoice)
            },
            CancellationToken.None,
            includeUntracked: false);

        Assert.True(trackedOnly.IsSuccess);
        Assert.Single(mutation.Requests);
        Assert.Equal(fifo.Id, mutation.Requests[0].ProductId);
    }

    private static Product CreateStockProduct(string code) =>
        Product.Create(
            code,
            code,
            ProductType.Product,
            Money.Create(10m, Money.DefaultCurrency),
            VatRate.Standard,
            Guid.NewGuid(),
            isStockManaged: true).Value;

    private sealed class RecordingStockMutationService : IStockMutationService
    {
        public List<StockMutationRequest> Requests { get; } = new();

        public Result Apply(
            StockItem stockItem,
            StockMutationRequest request,
            Product? product = null,
            IStockTraceabilityStore? store = null) =>
            Result.Success();

        public Task<Result<StockMutationResult>> ApplyAsync(
            StockMutationRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(Result.Success(new StockMutationResult(Guid.NewGuid(), request.Quantity, request.UnitCost)));
        }

        public Task<Result> CreateOpeningValuationLayersAsync(
            Guid productId,
            CostingMethod costingMethod,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }
}
