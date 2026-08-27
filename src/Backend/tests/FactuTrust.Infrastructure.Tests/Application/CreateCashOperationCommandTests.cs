using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Notifications;
using FactuTrust.Application.Features.CashDesk.Commands;
using FactuTrust.Application.Features.CashDesk.Services;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Plan §6.4/§9.6 : garde du flag <c>CashDeskVatEnabled</c> à la création (rejette toute requête
/// portant un <c>VatRate</c> quand le flag est désactivé), transmission du taux au domaine (qui
/// porte les règles Credit + CashSalesReceipt et le rejet Traite), audit et DTO retourné conformes
/// au calculateur.
/// </summary>
public sealed class CreateCashOperationCommandTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<ICashOperationRepository> _repository = new();
    private readonly Mock<ICashOperationNumberGenerator> _numberGenerator = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IAuditService> _auditService = new();
    private readonly Mock<IPublisher> _publisher = new();

    public CreateCashOperationCommandTests()
    {
        _currentUser.SetupGet(u => u.TenantId).Returns(TenantId);
        _currentUser.SetupGet(u => u.UserId).Returns(UserId);

        _numberGenerator
            .Setup(g => g.ReserveNextNumberAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CashOperationType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, int year, CashOperationType type, CancellationToken _) =>
                CashOperationNumber.Create(
                    type == CashOperationType.Debit ? CashOperationNumber.DebitPrefix : CashOperationNumber.CreditPrefix,
                    year,
                    1).Value);

        _repository
            .Setup(r => r.AddAsync(It.IsAny<CashOperation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CashOperation op, CancellationToken _) => op);
    }

    private CreateCashOperationCommandHandler BuildHandler(bool vatEnabled) =>
        new(
            _repository.Object,
            _numberGenerator.Object,
            _currentUser.Object,
            _auditService.Object,
            _publisher.Object,
            Options.Create(new AccountingSettings { CashDeskVatEnabled = vatEnabled }));

    private static CreateCashOperationRequest CreditSalesRequest(VatRate? vatRate) => new()
    {
        OperationType = CashOperationType.Credit,
        OperationDate = new DateTime(2026, 8, 10),
        Amount = 1.000m,
        Method = PaymentMethod.Cash,
        Label = "Vente au comptant",
        RevenueCategory = CashRevenueCategory.CashSalesReceipt,
        VatRate = vatRate
    };

    [Fact]
    public async Task Handle_FlagOff_VatRatePresent_FailsWithVatNotEnabledMessage()
    {
        var handler = BuildHandler(vatEnabled: false);
        var command = new CreateCashOperationCommand(CreditSalesRequest(VatRate.Standard));

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be("La TVA caisse n'est pas activée");
        _repository.Verify(r => r.AddAsync(It.IsAny<CashOperation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FlagOn_VatRateOnDebit_FailsWithDomainError()
    {
        var handler = BuildHandler(vatEnabled: true);
        var request = new CreateCashOperationRequest
        {
            OperationType = CashOperationType.Debit,
            OperationDate = new DateTime(2026, 8, 10),
            Amount = 50.000m,
            Method = PaymentMethod.Cash,
            Label = "Paiement loyer",
            Category = CashExpenseCategory.RentPayment,
            VatRate = VatRate.Standard
        };

        var result = await handler.Handle(new CreateCashOperationCommand(request), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be("Le taux de TVA ne s'applique qu'aux encaissements");
        _repository.Verify(r => r.AddAsync(It.IsAny<CashOperation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FlagOn_VatRateOnCreditOtherCategory_FailsWithDomainError()
    {
        var handler = BuildHandler(vatEnabled: true);
        var request = new CreateCashOperationRequest
        {
            OperationType = CashOperationType.Credit,
            OperationDate = new DateTime(2026, 8, 10),
            Amount = 100.000m,
            Method = PaymentMethod.Cash,
            Label = "Encaissement créance client",
            RevenueCategory = CashRevenueCategory.ClientReceivablesReceipt,
            VatRate = VatRate.Standard
        };

        var result = await handler.Handle(new CreateCashOperationCommand(request), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be("Le taux de TVA ne s'applique qu'aux encaissements « ventes au comptant »");
        _repository.Verify(r => r.AddAsync(It.IsAny<CashOperation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FlagOn_CreditCashSalesReceipt_Standard19_Succeeds()
    {
        var handler = BuildHandler(vatEnabled: true);
        var request = CreditSalesRequest(VatRate.Standard);
        object? auditedNewValues = null;

        _auditService
            .Setup(a => a.LogAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Guid?, object?, object?, CancellationToken>(
                (_, _, _, _, newValues, _) => auditedNewValues = newValues)
            .Returns(Task.CompletedTask);

        var result = await handler.Handle(new CreateCashOperationCommand(request), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : null);
        var dto = result.Value;
        dto.VatRatePercent.Should().Be(19);

        var (expectedHt, expectedVat) = CashOperationVatCalculator.SplitTtc(1.000m, VatRate.Standard);
        dto.HtAmount.Should().Be(expectedHt);
        dto.VatAmount.Should().Be(expectedVat);

        _repository.Verify(r => r.AddAsync(It.IsAny<CashOperation>(), It.IsAny<CancellationToken>()), Times.Once);

        auditedNewValues.Should().NotBeNull();
        auditedNewValues!.GetType().GetProperty("vatRate")!.GetValue(auditedNewValues)
            .Should().Be(VatRate.Standard.ToString());

        _publisher.Verify(
            p => p.Publish(It.IsAny<CashOperationCreatedForAccountingNotification>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_TraiteMethod_FailsWithExplicitDomainError()
    {
        var handler = BuildHandler(vatEnabled: true);
        var request = new CreateCashOperationRequest
        {
            OperationType = CashOperationType.Debit,
            OperationDate = new DateTime(2026, 8, 10),
            Amount = 200.000m,
            Method = PaymentMethod.Traite,
            Label = "Paiement fournisseur",
            Category = CashExpenseCategory.SupplierInvoicePayment
        };

        var result = await handler.Handle(new CreateCashOperationCommand(request), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Contain("traite");
        _repository.Verify(r => r.AddAsync(It.IsAny<CashOperation>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
