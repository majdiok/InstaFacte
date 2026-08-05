using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.SupplierInvoices.Commands;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class CancelSupplierInvoiceCommandHandlerTests
{
    [Fact]
    public async Task Handle_StandaloneReceiptInvoice_ReversesReceiptOnly_NotPurchaseOrder()
    {
        var supplier = BuildSupplier();
        var warehouse = Warehouse.Create("WH1", "Principal").Value;
        var category = ProductCategory.Create("C", "Cat").Value;
        var price = Money.Create(100m);
        var product = Product.Create(
            "ART-SA", "Standalone", ProductType.Product, price, VatRate.Standard,
            category.Id, purchasePrice: price).Value;

        var receipt = PurchaseReceipt.Create(
            PurchaseReceiptNumber.Create("BR", 2026, 9),
            supplier,
            warehouse,
            new DateTime(2026, 4, 2)).Value;
        Assert.True(receipt.AddLine(product, 2m, price).IsSuccess);
        Assert.True(receipt.MarkValidated().IsSuccess);

        var receiptLine = receipt.Lines.First();
        var invoice = SupplierInvoice.CreateFromPurchaseReceipt(
            receipt,
            purchaseOrder: null,
            "FS-2026-000200",
            new DateTime(2026, 4, 5),
            [(receiptLine.Id, receiptLine.ReceivedNotInvoicedQuantity)]).Value;

        Assert.True(receipt.ApplyInvoicing([(receiptLine.Id, receiptLine.ReceivedQuantity)]).IsSuccess);

        var invoiceRepo = new Mock<ISupplierInvoiceRepository>();
        invoiceRepo
            .Setup(r => r.GetByIdWithLinesAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        invoiceRepo
            .Setup(r => r.UpdateAsync(invoice, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var poRepo = new Mock<IPurchaseOrderRepository>();
        var receiptRepo = new Mock<IPurchaseReceiptRepository>();
        receiptRepo
            .Setup(r => r.GetByIdWithLinesAsync(receipt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);
        receiptRepo
            .Setup(r => r.UpdateAsync(receipt, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var accounting = new Mock<IAccountingService>();
        accounting
            .Setup(a => a.ReverseSupplierInvoiceEntryAsync(
                invoice.Id, invoice.InvoiceNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FactuTrust.Domain.Common.Result.Success());

        var handler = new CancelSupplierInvoiceCommandHandler(
            invoiceRepo.Object,
            poRepo.Object,
            receiptRepo.Object,
            new Mock<IAuditService>().Object,
            accounting.Object);

        var result = await handler.Handle(
            new CancelSupplierInvoiceCommand(invoice.Id, "Erreur de saisie"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        poRepo.Verify(
            r => r.GetByIdWithLinesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        poRepo.Verify(
            r => r.UpdateAsync(It.IsAny<PurchaseOrder>(), It.IsAny<CancellationToken>()),
            Times.Never);
        receiptRepo.Verify(
            r => r.UpdateAsync(receipt, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(0m, receipt.Lines.First().InvoicedQuantity);
    }

    private static Supplier BuildSupplier()
    {
        var address = Address.Create("1 rue", "Tunis", "Tunis").Value;
        var email = Email.Create("s@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        return Supplier.Create("Fournisseur", SupplierType.Business, address, email, nif: nif).Value;
    }
}
