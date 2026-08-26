using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Invoices.Commands;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class RecordInvoicePaymentsBatchSessionTests
{
    [Fact]
    public async Task Batch_InheritsOpenSessionFromInvoice_WhenRequestOmitsIt()
    {
        var session = CashRegisterSession.Open(Guid.NewGuid(), Guid.NewGuid(), Money.Create(10m)).Value;
        var invoice = NewValidatedInvoice();
        invoice.AssignCashRegisterSession(session.Id);

        Payment? recorded = null;
        var handler = CreateHandler(invoice, session, p => recorded = p);

        var result = await handler.Handle(
            new RecordInvoicePaymentsBatchCommand(invoice.Id, new[]
            {
                new RecordInvoicePaymentRequest
                {
                    PaymentDate = DateTime.UtcNow.Date,
                    Amount = 10m,
                    Method = PaymentMethod.Card
                }
            }),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.NotNull(recorded);
        Assert.Equal(session.Id, recorded!.CashRegisterSessionId);
    }

    [Fact]
    public async Task Batch_ClosedSession_FailsWithPosSessionClosed()
    {
        var session = CashRegisterSession.Open(Guid.NewGuid(), Guid.NewGuid(), Money.Zero()).Value;
        Assert.True(session.Close(Guid.NewGuid(), Money.Zero(), Money.Zero(), Guid.NewGuid()).IsSuccess);
        var invoice = NewValidatedInvoice();
        invoice.AssignCashRegisterSession(session.Id);

        var handler = CreateHandler(invoice, session, _ => { });

        var result = await handler.Handle(
            new RecordInvoicePaymentsBatchCommand(invoice.Id, new[]
            {
                new RecordInvoicePaymentRequest
                {
                    PaymentDate = DateTime.UtcNow.Date,
                    Amount = 10m,
                    Method = PaymentMethod.Cash
                }
            }),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("POS_SESSION_CLOSED", result.Error.Code);
    }

    private static Invoice NewValidatedInvoice()
    {
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;
        var invoice = Invoice.Create(
            InvoiceNumber.Create("FAC", 2026, 88),
            client,
            new DateTime(2026, 8, 20)).Value;
        Assert.True(invoice.AddCustomLine(
            "Article", null, 1m, "Unité", Money.Create(100m), VatRate.Exempt).IsSuccess);
        Assert.True(invoice.Validate().IsSuccess);
        return invoice;
    }

    private static RecordInvoicePaymentsBatchCommandHandler CreateHandler(
        Invoice invoice,
        CashRegisterSession session,
        Action<Payment> onPayment)
    {
        var invoices = new Mock<IInvoiceRepository>();
        invoices
            .Setup(i => i.GetByIdWithLinesAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var payments = new Mock<IPaymentRepository>();
        payments
            .Setup(p => p.GetByInvoiceIdAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Payment>());
        payments
            .Setup(p => p.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Payment, CancellationToken>((payment, _) => onPayment(payment))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);

        var sessions = new Mock<ICashRegisterSessionRepository>();
        sessions
            .Setup(s => s.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());

        return new RecordInvoicePaymentsBatchCommandHandler(
            invoices.Object,
            payments.Object,
            new PassthroughTenantUnitOfWork(),
            currentUser.Object,
            new Mock<IAuditService>().Object,
            new Mock<IPublisher>().Object,
            Options.Create(new AccountingSettings()),
            sessions.Object);
    }

    private sealed class PassthroughTenantUnitOfWork : ITenantUnitOfWork
    {
        public Task<Result> ExecuteAsync(
            Func<CancellationToken, Task<Result>> action,
            CancellationToken cancellationToken = default)
            => action(cancellationToken);

        public Task<Result<T>> ExecuteAsync<T>(
            Func<CancellationToken, Task<Result<T>>> action,
            CancellationToken cancellationToken = default)
            => action(cancellationToken);
    }
}
