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

/// <summary>
/// Validates that paying a credit note (AVO) in cash creates a Debit cash operation
/// (the cash physically leaves the desk to refund the customer), as opposed to the
/// Credit operation generated when paying a regular invoice (FAC).
/// </summary>
public sealed class CreateCashOperationOnCreditNoteRefundTests
{
    [Fact]
    public async Task Handle_RefundOnCreditNote_CreatesDebitCashOperation()
    {
        var payment = CreateRefundPaymentForCreditNote();
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
            .Setup(x => x.ReserveNextNumberAsync(
                It.IsAny<Guid>(),
                payment.PaymentDate.Year,
                CashOperationType.Debit,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashOperationNumber.Create(CashOperationNumber.DebitPrefix, payment.PaymentDate.Year, 7).Value);

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
            op.OperationType == CashOperationType.Debit &&
            op.Method == PaymentMethod.Cash &&
            op.Label.StartsWith("Remboursement avoir")), It.IsAny<CancellationToken>()), Times.Once);

        // The standard Credit number must NOT be reserved for refunds.
        numberGenerator.Verify(x => x.ReserveNextNumberAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), CashOperationType.Credit, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Payment CreateRefundPaymentForCreditNote()
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;
        var invoice = Invoice.Create(
            number: InvoiceNumber.Create("AVO", 2026, 7),
            client: client,
            issueDate: new DateTime(2026, 5, 9),
            type: InvoiceType.CreditNote).Value;

        var paymentResult = Payment.Create(
            invoice,
            Money.Create(119.000m, Money.DefaultCurrency),
            paymentDate: new DateTime(2026, 5, 9),
            method: PaymentMethod.Cash,
            reference: "POS-REF");

        return paymentResult.Value;
    }
}
