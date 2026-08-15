using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.InvoiceWizard;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Contrat de la règle « Montant avoir » : TTC commercial hors timbre, millimes, Abs, tolérance.
/// </summary>
public sealed class InvoiceComplianceCreditNoteAmountTests
{
    [Fact]
    public async Task FullCopy_NoFodecNoStamp_IsValid()
    {
        var original = NewStandardInvoice();
        AddLine(original, qty: 2m, unitHt: 500m, fodec: false);
        Assert.True(original.Validate().IsSuccess);

        var draft = CreditNoteDraft(original.Id, new DraftInvoiceLine
        {
            Designation = "Article",
            Quantity = 2m,
            UnitPriceHT = 500m,
            VatRate = 19
        });

        var result = await Validator(original).ValidateAsync(draft);

        AssertAmount(result, "VALID");
        AssertLinked(result, "VALID");
    }

    [Fact]
    public async Task FullCopy_WithFodecAndStamp_IsValid()
    {
        var original = NewStandardInvoice();
        AddLine(original, qty: 10m, unitHt: 100m, fodec: true);
        SetStamp(original, 1m);
        Assert.True(original.Validate().IsSuccess);

        var draft = CreditNoteDraft(original.Id, new DraftInvoiceLine
        {
            Designation = "Article",
            Quantity = 10m,
            UnitPriceHT = 100m,
            VatRate = 19,
            FodecApplicable = true
        });

        var result = await Validator(original).ValidateAsync(draft);

        AssertAmount(result, "VALID");
    }

    [Fact]
    public async Task FullCopy_UnroundedVatLines_IsValid()
    {
        var original = NewStandardInvoice();
        AddLine(original, qty: 1m, unitHt: 10.001m, fodec: false);
        Assert.True(original.Validate().IsSuccess);

        var draft = CreditNoteDraft(original.Id, new DraftInvoiceLine
        {
            Designation = "Article",
            Quantity = 1m,
            UnitPriceHT = 10.001m,
            VatRate = 19
        });

        var result = await Validator(original).ValidateAsync(draft);

        AssertAmount(result, "VALID");
    }

    [Fact]
    public async Task FullCopy_OriginalHasGlobalDiscount_IsValid()
    {
        var original = NewStandardInvoice();
        AddLine(original, qty: 1m, unitHt: 1000m, fodec: false);
        Assert.True(original.SetGlobalDiscount(20m, null).IsSuccess);
        Assert.True(original.Validate().IsSuccess);

        var draft = CreditNoteDraft(original.Id, new DraftInvoiceLine
        {
            Designation = "Article",
            Quantity = 1m,
            UnitPriceHT = 1000m,
            DiscountType = "PERCENT",
            DiscountValue = 20m,
            VatRate = 19
        });

        var result = await Validator(original).ValidateAsync(draft);

        AssertAmount(result, "VALID");
    }

    [Fact]
    public async Task DraftExceedsOriginal_IsError()
    {
        var original = NewStandardInvoice();
        AddLine(original, qty: 1m, unitHt: 100m, fodec: false);
        Assert.True(original.Validate().IsSuccess);

        var draft = CreditNoteDraft(original.Id, new DraftInvoiceLine
        {
            Designation = "Article",
            Quantity = 2m,
            UnitPriceHT = 100m,
            VatRate = 19
        });

        var result = await Validator(original).ValidateAsync(draft);

        var check = AssertAmount(result, "ERROR");
        Assert.True(check.IsBlocking);
        Assert.Contains("dépasse", check.Description, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.CanProceed);
    }

    [Fact]
    public async Task LinkedCreditNote_IsLinkedInvoiceError()
    {
        var sourceAvo = Invoice.CreateCreditNote(
            InvoiceNumber.Create("AVO", 2026, 21),
            NewClient(),
            new DateTime(2026, 7, 20),
            Guid.NewGuid()).Value;
        AddLine(sourceAvo, qty: 1m, unitHt: 100m, fodec: false);
        Assert.True(sourceAvo.Validate().IsSuccess);

        var draft = CreditNoteDraft(sourceAvo.Id, new DraftInvoiceLine
        {
            Designation = "Article",
            Quantity = 1m,
            UnitPriceHT = 100m,
            VatRate = 19
        });

        var result = await Validator(sourceAvo).ValidateAsync(draft);

        var linked = AssertLinked(result, "ERROR");
        Assert.True(linked.IsBlocking);
        Assert.Contains("avoir", linked.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            result.Checks.Where(c => c.Id == "credit-note-amount" && c.Status == "ERROR"),
            c => true);
    }

    [Fact]
    public async Task LinkedDraftInvoice_IsLinkedInvoiceError()
    {
        var original = NewStandardInvoice();
        AddLine(original, qty: 1m, unitHt: 100m, fodec: false);
        Assert.Equal(InvoiceStatus.Draft, original.Status);

        var draft = CreditNoteDraft(original.Id, new DraftInvoiceLine
        {
            Designation = "Article",
            Quantity = 1m,
            UnitPriceHT = 100m,
            VatRate = 19
        });

        var result = await Validator(original).ValidateAsync(draft);

        var linked = AssertLinked(result, "ERROR");
        Assert.Contains("émise", linked.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingLinkedInvoice_AmountIsWarning_LinkedIsError()
    {
        var draft = InvoiceDraft.Create(InvoiceType.CreditNote);
        draft.UpdateMetadata(new DraftMetadata
        {
            Type = InvoiceType.CreditNote,
            IssueDate = new DateTime(2026, 8, 15),
            Currency = "TND"
        });
        draft.UpdateLines(new List<DraftInvoiceLine>
        {
            new() { Designation = "Article", Quantity = 1m, UnitPriceHT = 100m, VatRate = 19 }
        });

        var result = await Validator(linked: null).ValidateAsync(draft);

        AssertLinked(result, "ERROR");
        var amount = AssertAmount(result, "WARNING");
        Assert.False(amount.IsBlocking);
    }

    [Fact]
    public async Task StandardInvoiceDraft_DoesNotRunCreditNoteAmountCheck()
    {
        var draft = InvoiceDraft.Create(InvoiceType.Standard);
        draft.UpdateMetadata(new DraftMetadata
        {
            Type = InvoiceType.Standard,
            IssueDate = new DateTime(2026, 8, 15),
            Currency = "TND"
        });
        draft.UpdateLines(new List<DraftInvoiceLine>
        {
            new() { Designation = "Article", Quantity = 1m, UnitPriceHT = 100m, VatRate = 19 }
        });

        var result = await Validator(linked: null).ValidateAsync(draft);

        Assert.DoesNotContain(result.Checks, c => c.Id == "credit-note-amount");
        Assert.DoesNotContain(result.Checks, c => c.Id == "linked-invoice");
    }

    [Fact]
    public async Task SecondFullCopy_WhenPriorCreditNoteExists_IsError()
    {
        var original = NewStandardInvoice();
        AddLine(original, qty: 1m, unitHt: 100m, fodec: false);
        Assert.True(original.Validate().IsSuccess);

        var alreadyCredited = CreditNoteAmountGuard.CommercialTtcFromInvoice(original);
        var draft = CreditNoteDraft(original.Id, new DraftInvoiceLine
        {
            Designation = "Article",
            Quantity = 1m,
            UnitPriceHT = 100m,
            VatRate = 19
        });

        var result = await Validator(original, alreadyCredited).ValidateAsync(draft);

        var check = AssertAmount(result, "ERROR");
        Assert.Contains("cumul", check.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Guard_DoesNotExceed_AllowsMillimeTolerance()
    {
        Assert.True(CreditNoteAmountGuard.DoesNotExceed(11.901m, 11.901m));
        Assert.True(CreditNoteAmountGuard.DoesNotExceed(11.902m, 11.901m));
        Assert.False(CreditNoteAmountGuard.DoesNotExceed(11.903m, 11.901m));
    }

    private static WizardValidationCheckDto AssertAmount(
        FactuTrust.Application.DTOs.WizardValidationResultDto result, string status)
    {
        var check = Assert.Single(result.Checks.Where(c => c.Id == "credit-note-amount"));
        Assert.Equal(status, check.Status);
        return check;
    }

    private static WizardValidationCheckDto AssertLinked(
        FactuTrust.Application.DTOs.WizardValidationResultDto result, string status)
    {
        var check = Assert.Single(result.Checks.Where(c => c.Id == "linked-invoice"));
        Assert.Equal(status, check.Status);
        return check;
    }

    private static InvoiceComplianceValidator Validator(Invoice? linked, decimal alreadyCredited = 0m)
    {
        var invoices = new Mock<IInvoiceRepository>();
        if (linked != null)
        {
            invoices
                .Setup(x => x.GetByIdAsync(linked.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(linked);
        }

        invoices
            .Setup(x => x.SumIssuedCreditNoteCommercialTtcAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(alreadyCredited);

        return new InvoiceComplianceValidator(
            new Mock<ICompanyRepository>().Object,
            new Mock<IClientRepository>().Object,
            invoices.Object,
            NullLogger<InvoiceComplianceValidator>.Instance,
            Options.Create(new AccountingSettings { FodecRatePercent = 1.0m }));
    }

    private static InvoiceDraft CreditNoteDraft(Guid linkedInvoiceId, params DraftInvoiceLine[] lines)
    {
        var draft = InvoiceDraft.Create(InvoiceType.CreditNote);
        draft.UpdateMetadata(new DraftMetadata
        {
            Type = InvoiceType.CreditNote,
            IssueDate = new DateTime(2026, 8, 15),
            DueDate = new DateTime(2026, 9, 14),
            Currency = "TND",
            LinkedInvoiceId = linkedInvoiceId
        });
        draft.UpdateLines(lines.ToList());
        return draft;
    }

    private static Client NewClient()
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        return Client.Create("Société test", ClientType.Business, address, email, nif).Value;
    }

    private static Invoice NewStandardInvoice() =>
        Invoice.Create(
            InvoiceNumber.Create("FAC", 2026, 21),
            NewClient(),
            new DateTime(2026, 7, 20),
            type: InvoiceType.Standard).Value;

    private static void AddLine(Invoice invoice, decimal qty, decimal unitHt, bool fodec)
    {
        var add = invoice.AddCustomLine(
            "Article conformité",
            null,
            qty,
            "Unité",
            Money.Create(unitHt),
            VatRate.Standard,
            discountPercent: null,
            isFodecApplicable: fodec,
            fodecRatePercent: 1m);
        Assert.True(add.IsSuccess, add.Error?.Description);
    }

    private static void SetStamp(Invoice invoice, decimal signedAmount)
    {
        var set = invoice.SetFiscalStampAmount(Money.FromSignedAmount(signedAmount));
        Assert.True(set.IsSuccess, set.Error?.Description);
    }
}
