using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Stock.EventHandlers;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;
using FactuTrust.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Tests de caractérisation (vague 0, lot A) — routage de la déduction de stock à la
/// validation d'une facture de vente.
///
/// Ils figent les branches d'aiguillage du handler AVANT le lot B, pour qu'aucune d'elles ne
/// disparaisse par effet de bord quand le garde-fou « facture issue d'un BL » sera remplacé
/// par la clé étrangère typée <c>Invoice.SourceDeliveryNoteId</c>.
///
/// Le test <see cref="Handle_WhenReferenceStartsWithBl_SkipsDeduction_KnownDefect"/> documente
/// le défaut corrigé au lot B : il est le seul de ce fichier destiné à être remplacé.
/// </summary>
public sealed class StockDeductionPathsCharacterizationTests
{
    [Fact]
    public async Task Handle_WhenCreditNote_ReturnsBeforeAnyRepositoryCall()
    {
        var notification = new InvoiceValidatedEvent(Guid.NewGuid(), "AVO-2026-000001", isCreditNote: true);
        var ctx = new HandlerContext();

        await ctx.Handler.Handle(notification, CancellationToken.None);

        ctx.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Handle_WhenMovementsAlreadyExistForReference_SkipsDeduction()
    {
        var notification = new InvoiceValidatedEvent(Guid.NewGuid(), "FAC-2026-000002", isCreditNote: false);
        var ctx = new HandlerContext();
        ctx.WithExistingMovementsFor($"Facture {notification.InvoiceNumber}");

        await ctx.Handler.Handle(notification, CancellationToken.None);

        ctx.InvoiceRepo.VerifyNoOtherCalls();
        ctx.StockItemRepo.VerifyNoOtherCalls();
        ctx.WarehouseRepo.VerifyNoOtherCalls();
        ctx.ProductRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenNoWarehouseConfigured_SkipsDeduction()
    {
        var product = NewProduct(stockManaged: true);
        var invoice = NewInvoiceWith(product, quantity: 3m);
        var notification = ValidatedEvent(invoice);

        var ctx = new HandlerContext();
        ctx.WithInvoice(invoice);
        ctx.WithNoDefaultWarehouse();

        await ctx.Handler.Handle(notification, CancellationToken.None);

        ctx.StockItemRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenProductNotStockManaged_DoesNotTouchStock()
    {
        var product = NewProduct(stockManaged: false);
        var invoice = NewInvoiceWith(product, quantity: 3m);
        var notification = ValidatedEvent(invoice);

        var ctx = new HandlerContext();
        ctx.WithInvoice(invoice);
        ctx.WithDefaultWarehouse();
        ctx.WithProduct(product);

        await ctx.Handler.Handle(notification, CancellationToken.None);

        ctx.StockItemRepo.Verify(
            x => x.GetByProductAndWarehouseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NominalCase_RecordsSaleExitWithInvoiceReference()
    {
        var product = NewProduct(stockManaged: true);
        var invoice = NewInvoiceWith(product, quantity: 4m);
        var notification = ValidatedEvent(invoice);

        var ctx = new HandlerContext();
        ctx.WithInvoice(invoice);
        var warehouse = ctx.WithDefaultWarehouse();
        ctx.WithProduct(product);
        var stockItem = ctx.WithStockItem(product.Id, warehouse.Id, onHand: 10m);

        await ctx.Handler.Handle(notification, CancellationToken.None);

        Assert.Equal(6m, stockItem.QuantityOnHand);

        var movement = stockItem.Movements.Last();
        Assert.Equal(MovementType.Exit, movement.Type);
        Assert.Equal(MovementReason.Sale, movement.Reason);
        Assert.Equal(-4m, movement.Quantity);
        Assert.Equal($"Facture {notification.InvoiceNumber}", movement.Reference);

        ctx.StockItemRepo.Verify(
            x => x.UpdateAsync(stockItem, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenStockInsufficient_DeductsOnlyWhatIsAvailable()
    {
        // Comportement volontairement conservé par le lot G : la vente n'est pas bloquée,
        // seule la traçabilité de l'écart est ajoutée.
        var product = NewProduct(stockManaged: true);
        var invoice = NewInvoiceWith(product, quantity: 10m);
        var notification = ValidatedEvent(invoice);

        var ctx = new HandlerContext();
        ctx.WithInvoice(invoice);
        var warehouse = ctx.WithDefaultWarehouse();
        ctx.WithProduct(product);
        var stockItem = ctx.WithStockItem(product.Id, warehouse.Id, onHand: 3m);

        await ctx.Handler.Handle(notification, CancellationToken.None);

        Assert.Equal(0m, stockItem.QuantityOnHand);
        Assert.Equal(-3m, stockItem.Movements.Last().Quantity);
    }

    [Fact]
    public async Task Handle_WhenStockInsufficient_RecordsShortfallAndAudits()
    {
        // Lot G : l'écart vendu/sorti devient une donnée de premier ordre, requêtable et
        // auditée, au lieu d'un simple avertissement de journal applicatif.
        var product = NewProduct(stockManaged: true);
        var invoice = NewInvoiceWith(product, quantity: 10m);
        var notification = ValidatedEvent(invoice);

        var ctx = new HandlerContext();
        ctx.WithInvoice(invoice);
        var warehouse = ctx.WithDefaultWarehouse();
        ctx.WithProduct(product);
        var stockItem = ctx.WithStockItem(product.Id, warehouse.Id, onHand: 3m);

        await ctx.Handler.Handle(notification, CancellationToken.None);

        var movement = stockItem.Movements.Last();
        Assert.Equal(7m, movement.ShortfallQuantity); // 10 demandés − 3 disponibles
        Assert.True(movement.HasShortfall);

        ctx.AuditService.Verify(
            x => x.LogAsync(
                AuditActions.Stock.DeductionShortfall,
                "Invoice",
                (Guid?)notification.InvoiceId,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NominalCase_LeavesShortfallNull()
    {
        var product = NewProduct(stockManaged: true);
        var invoice = NewInvoiceWith(product, quantity: 4m);
        var notification = ValidatedEvent(invoice);

        var ctx = new HandlerContext();
        ctx.WithInvoice(invoice);
        var warehouse = ctx.WithDefaultWarehouse();
        ctx.WithProduct(product);
        var stockItem = ctx.WithStockItem(product.Id, warehouse.Id, onHand: 10m);

        await ctx.Handler.Handle(notification, CancellationToken.None);

        var movement = stockItem.Movements.Last();
        Assert.Null(movement.ShortfallQuantity);
        Assert.False(movement.HasShortfall);
        ctx.AuditService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenInvoiceCameFromDeliveryNote_SkipsDeduction()
    {
        // Lot B : le garde-fou porte désormais sur la clé étrangère typée. Même avec une
        // référence entièrement personnalisée, le stock n'est pas décrémenté une seconde fois
        // (il l'a déjà été à la livraison).
        var product = NewProduct(stockManaged: true);
        var invoice = NewInvoiceWith(
            product,
            quantity: 4m,
            reference: "Cde 4471 — client Sfax",
            sourceDeliveryNoteId: Guid.NewGuid());
        var notification = ValidatedEvent(invoice);

        var ctx = new HandlerContext();
        ctx.WithInvoice(invoice);
        ctx.WithDefaultWarehouse();

        await ctx.Handler.Handle(notification, CancellationToken.None);

        ctx.ProductRepo.VerifyNoOtherCalls();
        ctx.StockItemRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenReferenceLooksLikeADeliveryNoteButIsADirectInvoice_StillDeducts()
    {
        // Lot B — non-régression symétrique : une facture directe dont la référence commence
        // par « BL » échappait à toute déduction de stock. Elle doit désormais être traitée
        // normalement, la référence n'ayant plus aucun rôle décisionnel.
        var product = NewProduct(stockManaged: true);
        var invoice = NewInvoiceWith(product, quantity: 4m, reference: "BL du 12/07 — saisie libre");
        var notification = ValidatedEvent(invoice);

        var ctx = new HandlerContext();
        ctx.WithInvoice(invoice);
        var warehouse = ctx.WithDefaultWarehouse();
        ctx.WithProduct(product);
        var stockItem = ctx.WithStockItem(product.Id, warehouse.Id, onHand: 10m);

        await ctx.Handler.Handle(notification, CancellationToken.None);

        Assert.Equal(6m, stockItem.QuantityOnHand);
    }

    // ─────────────────────────────── Fabriques de test ───────────────────────────────

    private static InvoiceValidatedEvent ValidatedEvent(Invoice invoice) =>
        new(invoice.Id, invoice.Number.Value, invoice.IsCreditNote);

    private static Product NewProduct(bool stockManaged) =>
        Product.Create(
            code: "P-STOCK",
            name: "Produit stocké",
            type: ProductType.Product,
            unitPrice: Money.Create(100m),
            vatRate: VatRate.Standard,
            categoryId: Guid.NewGuid(),
            unit: "Unité",
            isStockManaged: stockManaged).Value;

    private static Invoice NewInvoiceWith(
        Product product,
        decimal quantity,
        string? reference = null,
        Guid? sourceDeliveryNoteId = null)
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;
        var number = InvoiceNumber.Create("FAC", 2026, 42);
        var issueDate = new DateTime(2026, 7, 20);

        var result = sourceDeliveryNoteId.HasValue
            ? Invoice.CreateFromDeliveryNote(number, client, issueDate, sourceDeliveryNoteId.Value, reference: reference)
            : Invoice.Create(number, client, issueDate, reference: reference);

        Assert.True(result.IsSuccess, result.Error?.Description);
        var invoice = result.Value;

        Assert.True(invoice.AddLine(product, quantity).IsSuccess);
        return invoice;
    }

    /// <summary>Assemble le handler et ses cinq dépôts simulés.</summary>
    private sealed class HandlerContext
    {
        public Mock<IInvoiceRepository> InvoiceRepo { get; } = new();
        public Mock<IStockItemRepository> StockItemRepo { get; } = new();
        public Mock<IWarehouseRepository> WarehouseRepo { get; } = new();
        public Mock<IProductRepository> ProductRepo { get; } = new();
        public Mock<IStockMovementRepository> MovementRepo { get; } = new();
        public Mock<IAuditService> AuditService { get; } = new();

        public DeductStockOnInvoiceValidatedHandler Handler { get; }

        public HandlerContext()
        {
            MovementRepo
                .Setup(x => x.GetByReferenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<StockMovement>());

            Handler = new DeductStockOnInvoiceValidatedHandler(
                InvoiceRepo.Object,
                StockItemRepo.Object,
                WarehouseRepo.Object,
                ProductRepo.Object,
                MovementRepo.Object,
                AuditService.Object,
                NullLogger<DeductStockOnInvoiceValidatedHandler>.Instance);
        }

        public void WithExistingMovementsFor(string reference) =>
            MovementRepo
                .Setup(x => x.GetByReferenceAsync(reference, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { DummyMovement() });

        public void WithInvoice(Invoice invoice) =>
            InvoiceRepo
                .Setup(x => x.GetByIdWithLinesAsync(invoice.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(invoice);

        public Warehouse WithDefaultWarehouse()
        {
            var warehouse = Warehouse.Create("PRINCIPAL", "Entrepôt principal", isDefault: true).Value;
            WarehouseRepo
                .Setup(x => x.GetDefaultAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(warehouse);
            return warehouse;
        }

        public void WithNoDefaultWarehouse() =>
            WarehouseRepo
                .Setup(x => x.GetDefaultAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((Warehouse?)null);

        public void WithProduct(Product product) =>
            ProductRepo
                .Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);

        public StockItem WithStockItem(Guid productId, Guid warehouseId, decimal onHand)
        {
            var stockItem = StockItem.Create(productId, warehouseId).Value;
            Assert.True(stockItem.RecordEntry(onHand, 60m, MovementReason.InitialStock).IsSuccess);

            StockItemRepo
                .Setup(x => x.GetByProductAndWarehouseAsync(productId, warehouseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(stockItem);
            StockItemRepo
                .Setup(x => x.UpdateAsync(It.IsAny<StockItem>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            return stockItem;
        }

        public void VerifyNoRepositoryCalls()
        {
            InvoiceRepo.VerifyNoOtherCalls();
            StockItemRepo.VerifyNoOtherCalls();
            WarehouseRepo.VerifyNoOtherCalls();
            ProductRepo.VerifyNoOtherCalls();
            MovementRepo.VerifyNoOtherCalls();
        }

        private static StockMovement DummyMovement()
        {
            var stockItem = StockItem.Create(Guid.NewGuid(), Guid.NewGuid()).Value;
            Assert.True(stockItem.RecordEntry(1m, 10m, MovementReason.InitialStock).IsSuccess);
            return stockItem.Movements.Last();
        }
    }
}
