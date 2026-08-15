using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Pricing;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.InvoiceWizard.Commands;
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
/// Verifies the accounting routing added to the wizard submit path: a credit-note draft
/// posts GenerateInvoiceCreditNoteEntryAsync (mirror entry), a standard invoice posts
/// GenerateInvoiceSaleEntryAsync, and an accounting failure blocks the submission —
/// the same routing as ValidateInvoiceCommand.
/// </summary>
public sealed class SubmitInvoiceAccountingRoutingTests
{
    [Fact]
    public async Task Submit_CreditNoteDraft_GeneratesCreditNoteEntry()
    {
        var accounting = new Mock<IAccountingService>(MockBehavior.Strict);
        accounting
            .Setup(x => x.GenerateInvoiceCreditNoteEntryAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(global::FactuTrust.Domain.Common.Result.Success());

        var handler = CreateHandler(InvoiceType.CreditNote, accounting, out _);

        var result = await handler.Handle(
            new SubmitInvoiceCommand(CurrentDraft.Id, new string('k', 40)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        accounting.Verify(
            x => x.GenerateInvoiceCreditNoteEntryAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Submit_StandardDraft_GeneratesSaleEntry()
    {
        var accounting = new Mock<IAccountingService>(MockBehavior.Strict);
        accounting
            .Setup(x => x.GenerateInvoiceSaleEntryAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(global::FactuTrust.Domain.Common.Result.Success());

        var handler = CreateHandler(InvoiceType.Standard, accounting, out _);

        var result = await handler.Handle(
            new SubmitInvoiceCommand(CurrentDraft.Id, new string('k', 40)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        accounting.Verify(
            x => x.GenerateInvoiceSaleEntryAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Submit_AccountingFailure_FailsSubmission()
    {
        var accounting = new Mock<IAccountingService>(MockBehavior.Strict);
        accounting
            .Setup(x => x.GenerateInvoiceCreditNoteEntryAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(global::FactuTrust.Domain.Common.Result.Failure(
                global::FactuTrust.Domain.Common.Error.Validation("AccountNumber", "Le compte 4111 n'existe pas dans le plan comptable.")));

        var handler = CreateHandler(InvoiceType.CreditNote, accounting, out var unitOfWork);

        var result = await handler.Handle(
            new SubmitInvoiceCommand(CurrentDraft.Id, new string('k', 40)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        // Rien n'est commité quand l'écriture comptable échoue (facture + écriture atomiques) :
        // seul le SaveChanges de début de soumission (StartSubmission) a eu lieu — le commit
        // final (étape 13) n'est jamais atteint, la facture et l'écriture ne sont pas persistées.
        unitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Submit_CreditNote_KeepsDraftUnitPrice_NotCatalogPrice()
    {
        var catalogPrice = Money.Create(999m);
        var product = Product.Create(
            "ART-SNAP",
            "Article catalogue",
            ProductType.Service,
            catalogPrice,
            VatRate.Reduced,
            Guid.NewGuid(),
            isFodecApplicable: false).Value;

        Invoice? persisted = null;
        var accounting = new Mock<IAccountingService>(MockBehavior.Strict);
        accounting
            .Setup(x => x.GenerateInvoiceCreditNoteEntryAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(global::FactuTrust.Domain.Common.Result.Success());

        var handler = CreateHandler(InvoiceType.CreditNote, accounting, out _, product, invoice => persisted = invoice);

        var result = await handler.Handle(
            new SubmitInvoiceCommand(CurrentDraft.Id, new string('k', 40)),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.NotNull(persisted);
        var line = Assert.Single(persisted!.Lines);
        Assert.Equal(50m, line.UnitPrice.Amount);
        Assert.Equal(VatRate.Standard, line.VatRate);
        Assert.Equal(product.Id, line.ProductId);
    }

    private InvoiceDraft CurrentDraft => _draft!;
    private InvoiceDraft? _draft;

    private SubmitInvoiceCommandHandler CreateHandler(
        InvoiceType type,
        Mock<IAccountingService> accounting,
        out Mock<IUnitOfWork> unitOfWork,
        Product? catalogProduct = null,
        Action<Invoice>? onInvoiceAdded = null)
    {
        var client = Client.Create(
            "Ste Test",
            ClientType.Individual,
            Address.Create("1 rue Test", "Tunis", "Tunis", postalCode: "1000").Value,
            Email.Create("client@test.local").Value).Value;

        _draft = InvoiceDraft.Create(type);
        _draft.UpdateMetadata(new DraftMetadata
        {
            Type = type,
            IssueDate = new DateTime(2026, 9, 20),
            Currency = "TND",
            LinkedInvoiceId = type == InvoiceType.CreditNote ? Guid.NewGuid() : null
        });
        _draft.UpdateSeller(Guid.NewGuid());
        _draft.UpdateClient(client.Id, null);
        _draft.UpdateLines(new List<DraftInvoiceLine>
        {
            new()
            {
                ProductId = catalogProduct?.Id.ToString(),
                Designation = "Prestation de test",
                Quantity = 2,
                Unit = "Unité",
                UnitPriceHT = 50m,
                VatRate = 19,
                FodecApplicable = true
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

        var productRepository = new Mock<IProductRepository>();
        if (catalogProduct != null)
        {
            productRepository
                .Setup(x => x.GetByIdAsync(catalogProduct.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(catalogProduct);
        }

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

        return new SubmitInvoiceCommandHandler(
            draftRepository.Object,
            invoiceRepository.Object,
            clientRepository.Object,
            new Mock<ICompanyRepository>().Object,
            productRepository.Object,
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
            NullLogger<SubmitInvoiceCommandHandler>.Instance);
    }
}
