using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Pricing;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.InvoiceWizard.Commands;
using FactuTrust.Application.Features.Stock.Services;
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
/// Characterization: wizard submit must remain valid without a POS session (bureau),
/// stamp an open vacation, and reject a closed one.
/// </summary>
public sealed class SubmitInvoicePosSessionTests
{
    [Fact]
    public async Task Submit_WithoutCashRegisterSession_Succeeds()
    {
        Invoice? persisted = null;
        var handler = CreateHandler(null, out _, invoice => persisted = invoice);

        var result = await handler.Handle(
            new SubmitInvoiceCommand(CurrentDraft.Id, new string('k', 40)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.NotNull(persisted);
        Assert.Null(persisted!.CashRegisterSessionId);
    }

    [Fact]
    public async Task Submit_WithOpenSession_StampsInvoice()
    {
        var session = CashRegisterSession.Open(Guid.NewGuid(), Guid.NewGuid(), Money.Create(20m)).Value;
        Invoice? persisted = null;
        var handler = CreateHandler(session, out _, invoice => persisted = invoice);

        var result = await handler.Handle(
            new SubmitInvoiceCommand(CurrentDraft.Id, new string('k', 40)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(session.Id, persisted!.CashRegisterSessionId);
    }

    [Fact]
    public async Task Submit_WithClosedSession_FailsWithPosSessionClosed()
    {
        var session = CashRegisterSession.Open(Guid.NewGuid(), Guid.NewGuid(), Money.Zero()).Value;
        Assert.True(session.Close(Guid.NewGuid(), Money.Zero(), Money.Zero(), Guid.NewGuid()).IsSuccess);

        var handler = CreateHandler(session, out _);

        var result = await handler.Handle(
            new SubmitInvoiceCommand(CurrentDraft.Id, new string('k', 40)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("POS_SESSION_CLOSED", result.Error.Code);
    }

    private InvoiceDraft CurrentDraft => _draft!;
    private InvoiceDraft? _draft;

    private SubmitInvoiceCommandHandler CreateHandler(
        CashRegisterSession? session,
        out Mock<IUnitOfWork> unitOfWork,
        Action<Invoice>? onInvoiceAdded = null)
    {
        var client = Client.Create(
            "Ste Test",
            ClientType.Individual,
            Address.Create("1 rue Test", "Tunis", "Tunis", postalCode: "1000").Value,
            Email.Create("client@test.local").Value).Value;

        _draft = InvoiceDraft.Create(InvoiceType.Standard);
        _draft.UpdateMetadata(new DraftMetadata
        {
            Type = InvoiceType.Standard,
            IssueDate = new DateTime(2026, 9, 20),
            Currency = "TND",
            CashRegisterSessionId = session?.Id
        });
        _draft.UpdateSeller(Guid.NewGuid());
        _draft.UpdateClient(client.Id, null);
        _draft.UpdateLines(new List<DraftInvoiceLine>
        {
            new()
            {
                Designation = "Prestation de test",
                Quantity = 1,
                Unit = "Unité",
                UnitPriceHT = 50m,
                VatRate = 19,
                FodecApplicable = false
            }
        });
        _draft.UpdatePaymentLegal(new DraftPaymentLegal { PaymentMethod = "CASH" });

        var draftRepository = new Mock<IInvoiceDraftRepository>();
        draftRepository
            .Setup(x => x.GetByIdAsync(_draft.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_draft);

        var invoiceRepository = new Mock<IInvoiceRepository>();
        invoiceRepository
            .Setup(x => x.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .Callback<Invoice, CancellationToken>((invoice, _) => onInvoiceAdded?.Invoke(invoice))
            .ReturnsAsync((Invoice invoice, CancellationToken _) => invoice);

        var clientRepository = new Mock<IClientRepository>();
        clientRepository
            .Setup(x => x.GetByIdAsync(client.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        var numberGenerator = new Mock<IInvoiceNumberGenerator>();
        numberGenerator
            .Setup(x => x.ReserveNextNumberAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string prefix, int year, CancellationToken _) =>
                InvoiceNumber.Create(prefix, year, 1));

        var complianceValidator = new Mock<IInvoiceComplianceValidator>();
        complianceValidator
            .Setup(x => x.ValidateAsync(It.IsAny<InvoiceDraft>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WizardValidationResultDto { IsValid = true, CanProceed = true });

        unitOfWork = new Mock<IUnitOfWork>();

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());

        var fiscalStampResolver = new Mock<IFiscalStampResolver>();
        fiscalStampResolver
            .Setup(x => x.ResolveSignedStampAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((bool isCreditNote, CancellationToken _) =>
                Money.FromSignedAmount(isCreditNote ? -1m : 1m, "TND"));

        var trackedStock = new Mock<ITrackedDocumentStockService>();
        trackedStock
            .Setup(x => x.ApplyExitsAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<MovementReason>(),
                It.IsAny<IReadOnlyList<TrackedDocumentLine>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(global::FactuTrust.Domain.Common.Result.Success());

        var accounting = new Mock<IAccountingService>(MockBehavior.Strict);
        accounting
            .Setup(x => x.GenerateInvoiceSaleEntryAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(global::FactuTrust.Domain.Common.Result.Success());

        var sessions = new Mock<ICashRegisterSessionRepository>();
        if (session is not null)
        {
            sessions
                .Setup(s => s.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);
        }

        return new SubmitInvoiceCommandHandler(
            draftRepository.Object,
            invoiceRepository.Object,
            clientRepository.Object,
            new Mock<ICompanyRepository>().Object,
            new Mock<IProductRepository>().Object,
            numberGenerator.Object,
            complianceValidator.Object,
            unitOfWork.Object,
            currentUser.Object,
            new Mock<IAuditService>().Object,
            new Mock<IWarehouseRepository>().Object,
            fiscalStampResolver.Object,
            new Mock<ILinePricingOrchestrator>().Object,
            accounting.Object,
            Options.Create(new AccountingSettings()),
            NullLogger<SubmitInvoiceCommandHandler>.Instance,
            sessions.Object,
            trackedStock.Object,
            new Mock<IRecurringContractInvoiceLinker>().Object,
            new Mock<IStockMovementRepository>().Object,
            new Mock<IStockItemRepository>().Object);
    }
}
