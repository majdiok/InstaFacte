using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Forecasting;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Services.PurchaseOrders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Forecasting;

/// <summary>
/// Covers the additive <c>UnlinkedRecommendationIds</c> reporting on
/// <see cref="PurchaseOrderDraftFactory"/>. The skip logic itself is unchanged — these tests pin
/// the contract that recommendations which cannot be attached to a draft PO (no supplier resolved,
/// supplier missing/inactive) are surfaced to the caller, while a resolvable line still produces a
/// Draft PO with no false unlinked entry. This is the backend half of the "PO never appears as
/// Brouillon" fix: the root cause is a supplier-less recommendation, and the factory must say so.
/// </summary>
public sealed class PurchaseOrderDraftFactoryTests
{
    private static PurchaseOrderDraftFactory CreateFactory()
    {
        var repo = new Mock<IPurchaseOrderRepository>();
        repo.Setup(r => r.GetLatestNumberAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        return new PurchaseOrderDraftFactory(repo.Object, NullLogger<PurchaseOrderDraftFactory>.Instance);
    }

    private static Supplier CreateSupplier(string name = "Fournisseur Test")
    {
        var address = Address.Create("1 rue test", "Tunis", "Tunis").Value;
        var email = Email.Create("supplier.draft@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        return Supplier.Create(name, SupplierType.Business, address, email, nif: nif).Value;
    }

    private static Product CreateProduct(string code = "PR-1")
    {
        var category = ProductCategory.Create("CAT", "Catégorie test").Value;
        var unitPrice = Money.Create(100m, Money.DefaultCurrency);
        return Product.Create(
            code,
            "Produit " + code,
            ProductType.Product,
            unitPrice,
            VatRate.Standard,
            category.Id,
            purchasePrice: unitPrice).Value;
    }

    /// <summary>Builds a recommendation for <paramref name="productId"/>, optionally giving it a
    /// preferred supplier (mirrors the real V2 enrichment path).</summary>
    private static ReplenishmentRecommendation CreateRec(
        Guid productId, Guid? supplierId = null, string? supplierName = null)
    {
        var rec = ReplenishmentRecommendation.Create(
            productId: productId,
            warehouseId: Guid.NewGuid(),
            recommendedQty: 100m,
            rop: 50m,
            safetyStock: 20m,
            leadTimeDays: 7,
            dailyDemand: 5m,
            reasonCodesJson: "[\"BelowSafetyStock\"]");
        if (supplierId is not null)
            rec.SetV2Enrichment(supplierId, supplierName, quantityOnHand: 5m, quantityOnOrder: 0m, daysOfStockRemaining: 1m);
        return rec;
    }

    [Fact]
    public async Task BuildDraftPurchaseOrders_WithResolvedActiveSupplier_BuildsDraftAndReportsNoUnlinked()
    {
        var factory = CreateFactory();
        var supplier = CreateSupplier();
        var product = CreateProduct();
        var rec = CreateRec(product.Id, supplier.Id, supplier.Name);

        var result = await factory.BuildDraftPurchaseOrdersAsync(
            new[] { rec },
            new Dictionary<Guid, Product> { [product.Id] = product },
            new Dictionary<Guid, Supplier> { [supplier.Id] = supplier },
            actorUserId: "user-1");

        var draft = Assert.Single(result.Drafts);
        Assert.Equal(PurchaseOrderStatus.Draft, draft.PurchaseOrder.Status);
        Assert.Equal(supplier.Id, draft.PurchaseOrder.SupplierId);
        Assert.Single(draft.PurchaseOrder.Lines);
        Assert.Equal(rec.Id, Assert.Single(draft.RecommendationIds));
        Assert.Empty(result.UnlinkedRecommendationIds);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task BuildDraftPurchaseOrders_WithNoSupplier_ReportsUnlinkedAndBuildsNoDraft()
    {
        var factory = CreateFactory();
        var product = CreateProduct();
        var rec = CreateRec(product.Id, supplierId: null);

        var result = await factory.BuildDraftPurchaseOrdersAsync(
            new[] { rec },
            new Dictionary<Guid, Product> { [product.Id] = product },
            new Dictionary<Guid, Supplier>(),
            actorUserId: "user-1");

        Assert.Empty(result.Drafts);
        Assert.Equal(rec.Id, Assert.Single(result.UnlinkedRecommendationIds));
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task BuildDraftPurchaseOrders_WithSupplierMissingFromDictionary_ReportsUnlinked()
    {
        var factory = CreateFactory();
        var product = CreateProduct();
        // Recommendation references a supplier id that the caller did not supply in the dictionary.
        var rec = CreateRec(product.Id, supplierId: Guid.NewGuid(), supplierName: "Fantôme");

        var result = await factory.BuildDraftPurchaseOrdersAsync(
            new[] { rec },
            new Dictionary<Guid, Product> { [product.Id] = product },
            new Dictionary<Guid, Supplier>(),
            actorUserId: "user-1");

        Assert.Empty(result.Drafts);
        Assert.Equal(rec.Id, Assert.Single(result.UnlinkedRecommendationIds));
    }

    [Fact]
    public async Task BuildDraftPurchaseOrders_WithInactiveSupplier_ReportsUnlinked()
    {
        var factory = CreateFactory();
        var supplier = CreateSupplier();
        supplier.Deactivate();
        var product = CreateProduct();
        var rec = CreateRec(product.Id, supplier.Id, supplier.Name);

        var result = await factory.BuildDraftPurchaseOrdersAsync(
            new[] { rec },
            new Dictionary<Guid, Product> { [product.Id] = product },
            new Dictionary<Guid, Supplier> { [supplier.Id] = supplier },
            actorUserId: "user-1");

        Assert.Empty(result.Drafts);
        Assert.Equal(rec.Id, Assert.Single(result.UnlinkedRecommendationIds));
    }

    [Fact]
    public async Task BuildDraftPurchaseOrders_MixedSelection_LinksResolvableAndReportsTheRest()
    {
        var factory = CreateFactory();
        var supplier = CreateSupplier();
        var withSupplier = CreateProduct("PR-WITH");
        var withoutSupplier = CreateProduct("PR-WITHOUT");
        var recLinked = CreateRec(withSupplier.Id, supplier.Id, supplier.Name);
        var recUnlinked = CreateRec(withoutSupplier.Id, supplierId: null);

        var result = await factory.BuildDraftPurchaseOrdersAsync(
            new[] { recLinked, recUnlinked },
            new Dictionary<Guid, Product> { [withSupplier.Id] = withSupplier, [withoutSupplier.Id] = withoutSupplier },
            new Dictionary<Guid, Supplier> { [supplier.Id] = supplier },
            actorUserId: "user-1");

        var draft = Assert.Single(result.Drafts);
        Assert.Equal(recLinked.Id, Assert.Single(draft.RecommendationIds));
        Assert.Equal(recUnlinked.Id, Assert.Single(result.UnlinkedRecommendationIds));
    }

    [Fact]
    public async Task BuildDraftPurchaseOrders_WithEmptyInput_ReturnsEmptyResult()
    {
        var factory = CreateFactory();

        var result = await factory.BuildDraftPurchaseOrdersAsync(
            Array.Empty<ReplenishmentRecommendation>(),
            new Dictionary<Guid, Product>(),
            new Dictionary<Guid, Supplier>(),
            actorUserId: "user-1");

        Assert.Empty(result.Drafts);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.UnlinkedRecommendationIds);
    }
}
