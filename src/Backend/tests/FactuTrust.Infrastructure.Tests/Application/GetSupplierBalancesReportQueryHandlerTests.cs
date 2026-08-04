using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Reports.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Freezes the commercial supplier balance formula:
/// Balance = Σ TotalAmount (non-cancelled) − Σ SupplierPayment amounts (from repository).
/// </summary>
public sealed class GetSupplierBalancesReportQueryHandlerTests
{
    [Fact]
    public async Task PaidInvoice_YieldsZeroBalance()
    {
        var invoice = await NewSupplierInvoiceAsync("Fournisseur A", lineHt: 1000m);
        var paid = new Dictionary<Guid, decimal> { [invoice.Id] = invoice.TotalAmount.Amount };

        var result = await HandleAsync([invoice], paid);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal(invoice.TotalAmount.Amount, row.TotalInvoiced);
        Assert.Equal(invoice.TotalAmount.Amount, row.TotalPaid);
        Assert.Equal(0m, row.Balance);
        Assert.Equal("TND", row.Currency);
    }

    [Fact]
    public async Task PartialPayment_YieldsRemainingBalance()
    {
        var invoice = await NewSupplierInvoiceAsync("Fournisseur A", lineHt: 1000m);
        var paid = new Dictionary<Guid, decimal> { [invoice.Id] = 500m };

        var result = await HandleAsync([invoice], paid);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal(invoice.TotalAmount.Amount, row.TotalInvoiced);
        Assert.Equal(500m, row.TotalPaid);
        Assert.Equal(invoice.TotalAmount.Amount - 500m, row.Balance);
    }

    [Fact]
    public async Task CancelledInvoice_IsExcluded()
    {
        var active = await NewSupplierInvoiceAsync("Fournisseur A", lineHt: 1000m, invoiceNumber: "FS-1");
        var cancelled = await NewSupplierInvoiceAsync("Fournisseur A", lineHt: 500m, invoiceNumber: "FS-2", supplier: active.Supplier);
        Assert.True(cancelled.Cancel("Annulation test").IsSuccess);

        var paid = new Dictionary<Guid, decimal>
        {
            [active.Id] = 0m,
            [cancelled.Id] = 0m
        };

        var result = await HandleAsync([active, cancelled], paid);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal(active.TotalAmount.Amount, row.TotalInvoiced);
        Assert.Equal(0m, row.TotalPaid);
        Assert.Equal(active.TotalAmount.Amount, row.Balance);
    }

    [Fact]
    public async Task MultipleInvoices_AggregatePerSupplier()
    {
        var a1 = await NewSupplierInvoiceAsync("Fournisseur A", lineHt: 1000m, invoiceNumber: "FS-A1");
        var a2 = await NewSupplierInvoiceAsync("Fournisseur A", lineHt: 500m, invoiceNumber: "FS-A2", supplier: a1.Supplier);
        var b1 = await NewSupplierInvoiceAsync("Fournisseur B", lineHt: 200m, invoiceNumber: "FS-B1");

        var paid = new Dictionary<Guid, decimal>
        {
            [a1.Id] = 200m,
            [a2.Id] = 100m,
            [b1.Id] = 0m
        };

        var result = await HandleAsync([a1, a2, b1], paid);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);

        var rowA = result.Value.Single(r => r.SupplierName == "Fournisseur A");
        Assert.Equal(a1.TotalAmount.Amount + a2.TotalAmount.Amount, rowA.TotalInvoiced);
        Assert.Equal(300m, rowA.TotalPaid);
        Assert.Equal(a1.TotalAmount.Amount + a2.TotalAmount.Amount - 300m, rowA.Balance);

        var rowB = result.Value.Single(r => r.SupplierName == "Fournisseur B");
        Assert.Equal(b1.TotalAmount.Amount, rowB.TotalInvoiced);
        Assert.Equal(0m, rowB.TotalPaid);
        Assert.Equal(b1.TotalAmount.Amount, rowB.Balance);
    }

    [Fact]
    public async Task MissingPaidEntry_TreatedAsZero()
    {
        var invoice = await NewSupplierInvoiceAsync("Fournisseur A", lineHt: 1000m);
        var paid = new Dictionary<Guid, decimal>(); // no entry for invoice

        var result = await HandleAsync([invoice], paid);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal(0m, row.TotalPaid);
        Assert.Equal(invoice.TotalAmount.Amount, row.Balance);
    }

    [Fact]
    public async Task EmptyInvoices_ReturnsEmptyList()
    {
        var result = await HandleAsync([], new Dictionary<Guid, decimal>());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    private static async Task<Result<IReadOnlyList<SupplierBalanceReportRowDto>>> HandleAsync(
        IReadOnlyList<SupplierInvoice> invoices,
        IReadOnlyDictionary<Guid, decimal> paidByInvoice)
    {
        var invoiceRepo = new Mock<ISupplierInvoiceRepository>();
        invoiceRepo
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoices);

        var paymentRepo = new Mock<ISupplierPaymentRepository>();
        paymentRepo
            .Setup(x => x.GetTotalPaidBySupplierInvoiceIdsAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
            {
                var dict = new Dictionary<Guid, decimal>();
                foreach (var id in ids)
                {
                    if (paidByInvoice.TryGetValue(id, out var paid))
                        dict[id] = paid;
                }
                return dict;
            });

        var handler = new GetSupplierBalancesReportQueryHandler(invoiceRepo.Object, paymentRepo.Object);
        return await handler.Handle(new GetSupplierBalancesReportQuery(), CancellationToken.None);
    }

    private static Task<SupplierInvoice> NewSupplierInvoiceAsync(
        string supplierName,
        decimal lineHt,
        string invoiceNumber = "FS-2026-000001",
        Supplier? supplier = null)
    {
        supplier ??= CreateSupplier(supplierName);
        var category = ProductCategory.Create("BAL", "Catégorie soldes").Value;
        var unitPrice = Money.Create(lineHt);
        var product = Product.Create(
            $"PROD-{invoiceNumber}",
            "Produit solde",
            ProductType.Product,
            unitPrice,
            VatRate.Standard,
            category.Id,
            purchasePrice: unitPrice).Value;

        var po = PurchaseOrder.Create(
            PurchaseOrderNumber.Create("BC", 2026, Random.Shared.Next(1, 999999)),
            supplier,
            new DateTime(2026, 4, 1)).Value;

        Assert.True(po.AddLine(product, 1m).IsSuccess);
        Assert.True(po.Confirm().IsSuccess);
        var lineId = po.Lines.First().Id;
        Assert.True(po.ReceiveGoods([(lineId, 1m)]).IsSuccess);

        var lineSelections = po.Lines
            .Where(l => l.ReceivedNotInvoicedQuantity > 0)
            .Select(l => (l.Id, l.ReceivedNotInvoicedQuantity))
            .ToList();

        var invoice = SupplierInvoice.CreateFromPurchaseOrder(
            po,
            invoiceNumber,
            new DateTime(2026, 4, 5),
            lineSelections).Value;

        return Task.FromResult(invoice);
    }

    private static Supplier CreateSupplier(string name)
    {
        var address = Address.Create("1 rue Fournisseur", "Sfax", "Sfax").Value;
        var email = Email.Create($"{name.Replace(" ", "").ToLowerInvariant()}@example.com").Value;
        return Supplier.Create(name, SupplierType.Individual, address, email).Value;
    }
}
