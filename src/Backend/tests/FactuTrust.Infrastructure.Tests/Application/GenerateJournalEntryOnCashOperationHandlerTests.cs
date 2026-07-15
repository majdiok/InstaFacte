using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Accounting.EventHandlers;
using FactuTrust.Application.Features.Accounting.Notifications;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class GenerateJournalEntryOnCashOperationHandlerTests
{
    [Fact]
    public async Task Handle_WhenOriginIsInvoicePayment_ShouldSkipAccountingEntry()
    {
        var operationResult = CashOperation.CreateFromInvoicePayment(
            number: CashOperationNumber.Create(CashOperationNumber.CreditPrefix, 2026, 10).Value,
            paymentId: Guid.NewGuid(),
            invoiceNumber: "FAC-2026-000010",
            amount: Money.Create(200m, Money.DefaultCurrency),
            paymentDate: new DateTime(2026, 4, 20));

        Assert.True(operationResult.IsSuccess, operationResult.Error?.Description);

        var repository = new Mock<ICashOperationRepository>();
        repository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(operationResult.Value);

        var accountingService = new Mock<IAccountingService>();

        var handler = new GenerateJournalEntryOnCashOperationHandler(
            repository.Object,
            accountingService.Object,
            NullLogger<GenerateJournalEntryOnCashOperationHandler>.Instance);

        await handler.Handle(new CashOperationCreatedForAccountingNotification(operationResult.Value.Id), CancellationToken.None);

        accountingService.Verify(
            x => x.GenerateCashOperationEntryAsync(It.IsAny<CashOperation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOriginIsSupplierPayment_ShouldSkipAccountingEntry()
    {
        var operationResult = CashOperation.CreateFromSupplierPayment(
            number: CashOperationNumber.Create(CashOperationNumber.DebitPrefix, 2026, 44).Value,
            supplierPaymentId: Guid.NewGuid(),
            supplierInvoiceNumber: "FS-2026-000044",
            amount: Money.Create(150m, Money.DefaultCurrency),
            paymentDate: new DateTime(2026, 4, 21));

        Assert.True(operationResult.IsSuccess, operationResult.Error?.Description);

        var repository = new Mock<ICashOperationRepository>();
        repository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(operationResult.Value);

        var accountingService = new Mock<IAccountingService>();

        var handler = new GenerateJournalEntryOnCashOperationHandler(
            repository.Object,
            accountingService.Object,
            NullLogger<GenerateJournalEntryOnCashOperationHandler>.Instance);

        await handler.Handle(new CashOperationCreatedForAccountingNotification(operationResult.Value.Id), CancellationToken.None);

        accountingService.Verify(
            x => x.GenerateCashOperationEntryAsync(It.IsAny<CashOperation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
