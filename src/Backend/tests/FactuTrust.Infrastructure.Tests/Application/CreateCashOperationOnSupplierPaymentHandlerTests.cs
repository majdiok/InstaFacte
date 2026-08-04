using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Accounting.Notifications;
using FactuTrust.Application.Features.CashDesk.EventHandlers;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class CreateCashOperationOnSupplierPaymentHandlerTests
{
    [Fact]
    public async Task Handle_WhenCashPaymentAndFeatureEnabled_ShouldCreateCashOperation()
    {
        var payment = CreateSupplierCashPayment();
        var paymentRepository = new Mock<ISupplierPaymentRepository>();
        paymentRepository
            .Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var cashRepository = new Mock<ICashOperationRepository>();
        cashRepository
            .Setup(x => x.ExistsBySourceAsync("SupplierPayment", payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        cashRepository
            .Setup(x => x.AddAsync(It.IsAny<CashOperation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CashOperation operation, CancellationToken _) => operation);

        var numberGenerator = new Mock<ICashOperationNumberGenerator>();
        numberGenerator
            .Setup(x => x.ReserveNextNumberAsync(It.IsAny<Guid>(), payment.PaymentDate.Year, CashOperationType.Debit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashOperationNumber.Create(CashOperationNumber.DebitPrefix, payment.PaymentDate.Year, 55).Value);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());

        var handler = new CreateCashOperationOnSupplierPaymentHandler(
            paymentRepository.Object,
            cashRepository.Object,
            numberGenerator.Object,
            currentUser.Object,
            NullLogger<CreateCashOperationOnSupplierPaymentHandler>.Instance,
            Options.Create(new CashDeskFeaturesOptions { AutoCashFromSupplierPayment = true }));

        await handler.Handle(new SupplierPaymentRecordedForAccountingNotification(payment.Id), CancellationToken.None);

        cashRepository.Verify(x => x.AddAsync(It.Is<CashOperation>(op =>
            op.Origin == CashOperationOrigin.SupplierPayment &&
            op.SourceType == "SupplierPayment" &&
            op.SourceId == payment.Id &&
            op.OperationType == CashOperationType.Debit &&
            op.Method == PaymentMethod.Cash &&
            op.Category == CashExpenseCategory.SupplierInvoicePayment), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPaymentIsNotCash_ShouldSkipCreation()
    {
        var payment = CreateSupplierCashPayment(PaymentMethod.BankTransfer);
        var paymentRepository = new Mock<ISupplierPaymentRepository>();
        paymentRepository
            .Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var cashRepository = new Mock<ICashOperationRepository>();
        var numberGenerator = new Mock<ICashOperationNumberGenerator>();
        var currentUser = new Mock<ICurrentUser>();

        var handler = new CreateCashOperationOnSupplierPaymentHandler(
            paymentRepository.Object,
            cashRepository.Object,
            numberGenerator.Object,
            currentUser.Object,
            NullLogger<CreateCashOperationOnSupplierPaymentHandler>.Instance,
            Options.Create(new CashDeskFeaturesOptions { AutoCashFromSupplierPayment = true }));

        await handler.Handle(new SupplierPaymentRecordedForAccountingNotification(payment.Id), CancellationToken.None);

        cashRepository.Verify(x => x.AddAsync(It.IsAny<CashOperation>(), It.IsAny<CancellationToken>()), Times.Never);
        numberGenerator.Verify(
            x => x.ReserveNextNumberAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CashOperationType>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenFeatureDisabled_ShouldSkipCreation()
    {
        var payment = CreateSupplierCashPayment();
        var paymentRepository = new Mock<ISupplierPaymentRepository>();
        paymentRepository
            .Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var cashRepository = new Mock<ICashOperationRepository>();
        var numberGenerator = new Mock<ICashOperationNumberGenerator>();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());

        var handler = new CreateCashOperationOnSupplierPaymentHandler(
            paymentRepository.Object,
            cashRepository.Object,
            numberGenerator.Object,
            currentUser.Object,
            NullLogger<CreateCashOperationOnSupplierPaymentHandler>.Instance,
            Options.Create(new CashDeskFeaturesOptions { AutoCashFromSupplierPayment = false }));

        await handler.Handle(new SupplierPaymentRecordedForAccountingNotification(payment.Id), CancellationToken.None);

        cashRepository.Verify(x => x.AddAsync(It.IsAny<CashOperation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCashOperationAlreadyExists_ShouldSkipCreation()
    {
        var payment = CreateSupplierCashPayment();
        var paymentRepository = new Mock<ISupplierPaymentRepository>();
        paymentRepository
            .Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var cashRepository = new Mock<ICashOperationRepository>();
        cashRepository
            .Setup(x => x.ExistsBySourceAsync("SupplierPayment", payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var numberGenerator = new Mock<ICashOperationNumberGenerator>();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());

        var handler = new CreateCashOperationOnSupplierPaymentHandler(
            paymentRepository.Object,
            cashRepository.Object,
            numberGenerator.Object,
            currentUser.Object,
            NullLogger<CreateCashOperationOnSupplierPaymentHandler>.Instance,
            Options.Create(new CashDeskFeaturesOptions { AutoCashFromSupplierPayment = true }));

        await handler.Handle(new SupplierPaymentRecordedForAccountingNotification(payment.Id), CancellationToken.None);

        cashRepository.Verify(x => x.AddAsync(It.IsAny<CashOperation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static SupplierPayment CreateSupplierCashPayment(PaymentMethod method = PaymentMethod.Cash)
    {
        var address = Address.Create("1 rue de test", "Tunis", "Tunis").Value;
        var email = Email.Create("supplier@test.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var supplier = Supplier.Create("Ste test FF", SupplierType.Business, address, email, nif: nif).Value;

        var category = ProductCategory.Create("GEN", "Général").Value;
        var unitPrice = Money.Create(100m, Money.DefaultCurrency);
        var product = Product.Create(
            "PR-FF-1",
            "Article test",
            ProductType.Product,
            unitPrice,
            VatRate.Standard,
            category.Id,
            purchasePrice: unitPrice).Value;

        var poNumber = PurchaseOrderNumber.Create("BC", 2026, 500001);
        var po = PurchaseOrder.Create(poNumber, supplier, new DateTime(2026, 4, 1)).Value;

        var addLine = po.AddLine(product, 2m);
        if (addLine.IsFailure)
            throw new InvalidOperationException(addLine.Error.Description);

        var confirm = po.Confirm();
        if (confirm.IsFailure)
            throw new InvalidOperationException(confirm.Error.Description);

        var lineId = po.Lines.First().Id;
        var receive = po.ReceiveGoods(new[] { (lineId, 2m) });
        if (receive.IsFailure)
            throw new InvalidOperationException(receive.Error.Description);

        var lineSelections = po.Lines
            .Where(l => l.ReceivedNotInvoicedQuantity > 0)
            .Select(l => (l.Id, l.ReceivedNotInvoicedQuantity))
            .ToList();

        var invoice = SupplierInvoice.CreateFromPurchaseOrder(
            po,
            "FS-2026-TEST-FF",
            new DateTime(2026, 4, 5),
            lineSelections);

        if (invoice.IsFailure)
            throw new InvalidOperationException(invoice.Error.Description);

        var inv = invoice.Value;
        var paymentResult = SupplierPayment.Create(
            inv,
            inv.TotalAmount,
            new DateTime(2026, 4, 10),
            method,
            reference: "REF-FF");

        if (paymentResult.IsFailure)
            throw new InvalidOperationException(paymentResult.Error.Description);

        return paymentResult.Value;
    }
}
