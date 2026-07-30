using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Vague 1, lot 7 — le validateur de conformité doit refuser une facture avec TVA pour un
/// client dont le régime supprime la TVA (exonéré, suspension art. 11, export), et exiger
/// une attestation en cours de validité <b>à la date d'émission</b> pour une suspension.
/// </summary>
public sealed class InvoiceComplianceVatRegimeTests
{
    private static readonly DateTime IssueDate = new(2026, 7, 20);

    [Fact]
    public async Task NormalClient_WithVat_IsValid()
    {
        var invoice = NewInvoice(ClientVatRegime.Normal);
        AddLine(invoice, VatRate.Standard);

        var check = await RegimeCheck(invoice);

        Assert.Equal("VALID", check.Status);
    }

    [Fact]
    public async Task ExemptClient_WithVatLine_IsRejected()
    {
        var invoice = NewInvoice(ClientVatRegime.Exempt);
        AddLine(invoice, VatRate.Standard);

        var check = await RegimeCheck(invoice);

        Assert.Equal("ERROR", check.Status);
        Assert.True(check.IsBlocking);
    }

    [Fact]
    public async Task ExemptClient_WithoutVat_IsValid()
    {
        var invoice = NewInvoice(ClientVatRegime.Exempt);
        AddLine(invoice, VatRate.Exempt);

        var check = await RegimeCheck(invoice);

        Assert.Equal("VALID", check.Status);
    }

    [Fact]
    public void Suspension_WithoutCertificate_IsRefusedByDomain()
    {
        // Le domaine garantit qu'un client en suspension porte toujours une attestation :
        // impossible de créer la situation « suspension sans attestation ». Le contrôle
        // correspondant du validateur reste une défense en profondeur.
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var client = Client.Create("Société test", ClientType.Business, address, email, nif).Value;

        var result = client.SetVatRegime(ClientVatRegime.Suspension, certificate: null);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task SuspensionClient_WithCertificateCoveringIssueDate_IsValid()
    {
        var cert = VatExemptionCertificate.Create(
            "ATT-2026-001", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)).Value;
        var invoice = NewInvoice(ClientVatRegime.Suspension, cert);
        AddLine(invoice, VatRate.Exempt);

        var check = await RegimeCheck(invoice);

        Assert.Equal("VALID", check.Status);
    }

    [Fact]
    public async Task SuspensionClient_WithCertificateNotCoveringIssueDate_IsRejected()
    {
        // Attestation expirée avant la date d'émission : la validité est jugée à l'émission,
        // pas à la date du jour.
        var cert = VatExemptionCertificate.Create(
            "ATT-2025-009", new DateTime(2025, 1, 1), new DateTime(2025, 12, 31)).Value;
        var invoice = NewInvoice(ClientVatRegime.Suspension, cert);
        AddLine(invoice, VatRate.Exempt);

        var check = await RegimeCheck(invoice);

        Assert.Equal("ERROR", check.Status);
        Assert.True(check.IsBlocking);
    }

    private static async Task<FactuTrust.Application.DTOs.WizardValidationCheckDto> RegimeCheck(Invoice invoice)
    {
        var result = await Validator().ValidateInvoiceAsync(invoice);
        return Assert.Single(result.Checks.Where(c => c.Id == "client-vat-regime"));
    }

    private static InvoiceComplianceValidator Validator() =>
        new(new Mock<ICompanyRepository>().Object,
            new Mock<IClientRepository>().Object,
            new Mock<IInvoiceRepository>().Object,
            NullLogger<InvoiceComplianceValidator>.Instance);

    private static Invoice NewInvoice(ClientVatRegime regime, VatExemptionCertificate? cert = null)
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var client = Client.Create("Société test", ClientType.Business, address, email, nif).Value;

        var setRegime = client.SetVatRegime(regime, cert);
        Assert.True(setRegime.IsSuccess, setRegime.Error?.Description);

        return Invoice.Create(
            InvoiceNumber.Create("FAC", 2026, 21),
            client,
            IssueDate,
            type: InvoiceType.Standard).Value;
    }

    private static void AddLine(Invoice invoice, VatRate vatRate)
    {
        var add = invoice.AddCustomLine(
            "Article régime TVA",
            null,
            2m,
            "Unité",
            Money.Create(100m),
            vatRate,
            null,
            isFodecApplicable: false,
            fodecRatePercent: 1m);

        Assert.True(add.IsSuccess, add.Error?.Description);
    }
}
