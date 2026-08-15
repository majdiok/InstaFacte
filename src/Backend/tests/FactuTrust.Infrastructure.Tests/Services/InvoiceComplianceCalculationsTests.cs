using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
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
/// Vague 0 — correctif 6, étape F1 : la règle « invoice-calculations » recalculait le TTC
/// comme HT + TVA, en ignorant le FODEC et le timbre fiscal, et sans gérer le signe des avoirs.
///
/// Elle n'avait jamais été exécutée (<c>ValidateInvoiceAsync</c> était du code mort), ce qui
/// masquait le défaut. La brancher telle quelle aurait rendu non validable toute facture
/// portant un timbre — soit la quasi-totalité. Ces tests verrouillent la correction avant
/// que l'étape F2 ne branche le validateur sur ValidateInvoiceCommand.
/// </summary>
public sealed class InvoiceComplianceCalculationsTests
{
    [Fact]
    public async Task Invoice_WithFiscalStamp_IsValid()
    {
        var invoice = NewInvoice(InvoiceType.Standard);
        AddLine(invoice, qty: 1m, unitHt: 1000m, fodec: false);
        SetStamp(invoice, 1m);

        var result = await Validator().ValidateInvoiceAsync(invoice);

        AssertCalculationsValid(result);
    }

    [Fact]
    public async Task Invoice_WithFodec_IsValid()
    {
        var invoice = NewInvoice(InvoiceType.Standard);
        AddLine(invoice, qty: 10m, unitHt: 100m, fodec: true);

        var result = await Validator().ValidateInvoiceAsync(invoice);

        AssertCalculationsValid(result);
    }

    [Fact]
    public async Task Invoice_WithFodecAndStampAndDiscount_IsValid()
    {
        var invoice = NewInvoice(InvoiceType.Standard);
        AddLine(invoice, qty: 7m, unitHt: 33.333m, fodec: true, discountPercent: 12.5m);
        AddLine(invoice, qty: 3m, unitHt: 150m, fodec: false, vatRate: VatRate.Reduced);
        SetStamp(invoice, 1m);

        var result = await Validator().ValidateInvoiceAsync(invoice);

        AssertCalculationsValid(result);
    }

    [Fact]
    public async Task CreditNote_WithNegativeHeaderAndSignedStamp_IsValid()
    {
        var invoice = NewInvoice(InvoiceType.CreditNote);
        AddLine(invoice, qty: 2m, unitHt: 250m, fodec: true);
        SetStamp(invoice, -1m); // le résolveur signe déjà le timbre d'un avoir

        var result = await Validator().ValidateInvoiceAsync(invoice);

        AssertCalculationsValid(result);
        Assert.True(invoice.TotalAmount.Amount < 0);
    }

    [Fact]
    public async Task Invoice_WithoutFodecOrStamp_IsValid()
    {
        var invoice = NewInvoice(InvoiceType.Standard);
        AddLine(invoice, qty: 2m, unitHt: 500m, fodec: false);

        var result = await Validator().ValidateInvoiceAsync(invoice);

        AssertCalculationsValid(result);
    }

    [Fact]
    public async Task AllBlockingChecks_PassOnAWellFormedInvoice()
    {
        var invoice = NewInvoice(InvoiceType.Standard);
        AddLine(invoice, qty: 1m, unitHt: 1000m, fodec: true);
        SetStamp(invoice, 1m);

        var result = await Validator().ValidateInvoiceAsync(invoice);

        // C'est la précondition de l'étape F2 : sans cela, brancher le validateur sur
        // ValidateInvoiceCommand bloquerait la validation de factures parfaitement valides.
        Assert.True(result.CanProceed,
            string.Join(" ; ", result.Checks.Where(c => c.Status == "ERROR").Select(c => c.Description)));
        Assert.Equal(0, result.ErrorCount);
    }

    private static void AssertCalculationsValid(FactuTrust.Application.DTOs.WizardValidationResultDto result)
    {
        var check = Assert.Single(result.Checks.Where(c => c.Id == "invoice-calculations"));
        Assert.Equal("VALID", check.Status);
    }

    private static InvoiceComplianceValidator Validator() =>
        new(new Mock<ICompanyRepository>().Object,
            new Mock<IClientRepository>().Object,
            new Mock<IInvoiceRepository>().Object,
            NullLogger<InvoiceComplianceValidator>.Instance,
            Options.Create(new AccountingSettings()));

    private static Invoice NewInvoice(InvoiceType type)
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var client = Client.Create("Société test", ClientType.Business, address, email, nif).Value;

        var prefix = type == InvoiceType.CreditNote ? "AVO" : "FAC";
        return Invoice.Create(
            InvoiceNumber.Create(prefix, 2026, 21),
            client,
            new DateTime(2026, 7, 20),
            type: type).Value;
    }

    private static void AddLine(
        Invoice invoice,
        decimal qty,
        decimal unitHt,
        bool fodec,
        decimal? discountPercent = null,
        VatRate vatRate = VatRate.Standard)
    {
        var add = invoice.AddCustomLine(
            "Article conformité",
            null,
            qty,
            "Unité",
            Money.Create(unitHt),
            vatRate,
            discountPercent,
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
