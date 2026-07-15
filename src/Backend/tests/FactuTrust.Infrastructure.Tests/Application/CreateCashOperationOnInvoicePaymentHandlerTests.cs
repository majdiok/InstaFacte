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

public sealed class CreateCashOperationOnInvoicePaymentHandlerTests
{
    [Fact]
    public async Task Handle_WhenCashPaymentAndFeatureEnabled_ShouldCreateCashOperation()
    {
        var payment = CreatePayment(PaymentMethod.Cash);
        var paymentRepository = new Mock<IPaymentRepository>();
        paymentRepository
            .Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var cashRepository = new Mock<ICashOperationRepository>();
        cashRepository
            .Setup(x => x.ExistsBySourceAsync("Payment", payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        cashRepository
            .Setup(x => x.AddAsync(It.IsAny<CashOperation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CashOperation operation, CancellationToken _) => operation);

        var numberGenerator = new Mock<ICashOperationNumberGenerator>();
        numberGenerator
            .Setup(x => x.ReserveNextNumberAsync(It.IsAny<Guid>(), payment.PaymentDate.Year, CashOperationType.Credit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashOperationNumber.Create(CashOperationNumber.CreditPrefix, payment.PaymentDate.Year, 123).Value);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());

        var handler = new CreateCashOperationOnInvoicePaymentHandler(
            paymentRepository.Object,
            cashRepository.Object,
            numberGenerator.Object,
            currentUser.Object,
            NullLogger<CreateCashOperationOnInvoicePaymentHandler>.Instance,
            Options.Create(new CashDeskFeaturesOptions { AutoCashFromInvoicePayment = true }));

        await handler.Handle(new InvoicePaymentRecordedNotification(payment.Id), CancellationToken.None);

        cashRepository.Verify(x => x.AddAsync(It.Is<CashOperation>(op =>
            op.Origin == CashOperationOrigin.InvoicePayment &&
            op.SourceType == "Payment" &&
            op.SourceId == payment.Id &&
            op.OperationType == CashOperationType.Credit &&
            op.Method == PaymentMethod.Cash), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPaymentIsNotCash_ShouldSkipCreation()
    {
        var payment = CreatePayment(PaymentMethod.BankTransfer);
        var paymentRepository = new Mock<IPaymentRepository>();
        paymentRepository
            .Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var cashRepository = new Mock<ICashOperationRepository>();
        var numberGenerator = new Mock<ICashOperationNumberGenerator>();
        var currentUser = new Mock<ICurrentUser>();

        var handler = new CreateCashOperationOnInvoicePaymentHandler(
            paymentRepository.Object,
            cashRepository.Object,
            numberGenerator.Object,
            currentUser.Object,
            NullLogger<CreateCashOperationOnInvoicePaymentHandler>.Instance,
            Options.Create(new CashDeskFeaturesOptions { AutoCashFromInvoicePayment = true }));

        await handler.Handle(new InvoicePaymentRecordedNotification(payment.Id), CancellationToken.None);

        cashRepository.Verify(x => x.AddAsync(It.IsAny<CashOperation>(), It.IsAny<CancellationToken>()), Times.Never);
        numberGenerator.Verify(x => x.ReserveNextNumberAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CashOperationType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Payment CreatePayment(PaymentMethod method)
    {
        var address = Address.Create("1 rue de la Republique", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;
        var invoice = Invoice.Create(
            InvoiceNumber.Create("FAC", 2026, 42),
            client,
            issueDate: new DateTime(2026, 4, 20),
            dueDate: new DateTime(2026, 4, 20)).Value;

        var paymentResult = Payment.Create(
            invoice,
            Money.Create(813.450m, Money.DefaultCurrency),
            paymentDate: new DateTime(2026, 4, 20),
            method: method,
            reference: "POS-CASH");

        return paymentResult.Value;
    }
}
