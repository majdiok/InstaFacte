using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Common.Models;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Services;
using FactuTrust.Infrastructure.Services.Templates;
using Microsoft.Extensions.Http;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class PdfServiceInvoiceTests
{
    [Fact]
    public async Task GenerateInvoicePdfAsync_WithMinimalInvoice_ReturnsNonEmptyPdf()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient());

        var registry = new DocumentTemplateRegistry(new IDocumentTemplate[] { new StandardDocumentTemplate() });
        var pdfService = new PdfService(httpFactory.Object, registry);

        var address = Address.Create("1 rue Test", "Tunis", "Tunis", null, "1000").Value;
        var email = Email.Create("client@test.tn").Value;
        var client = Client.Create("Client Test", ClientType.Individual, address, email).Value;

        var number = InvoiceNumber.Create("FAC", 2026, 1);
        var invoice = Invoice.Create(number, client, new DateTime(2026, 2, 18)).Value;
        Assert.True(invoice.AddCustomLine(
            "Prestation",
            "Détail ligne",
            2,
            "h",
            Money.Create(150.000m, "TND"),
            VatRate.Standard,
            null).IsSuccess);
        Assert.True(invoice.SetFiscalStampAmount(Money.FromSignedAmount(1m, "TND")).IsSuccess);
        Assert.True(invoice.Validate().IsSuccess);

        var companyAddress = Address.Create("Route KM 6", "Sfax", "Sfax").Value;
        var companyEmail = Email.Create("info@test.tn").Value;
        var nif = NIF.Create("1234567/A/B/C/000").Value;
        var company = Company.Create("SOLVIA TEST", companyAddress, nif, companyEmail, commerceRegistry: "RC1").Value;

        var ctx = new InvoicePdfContext(invoice, company, "DV-2026-0017");

        var bytes = await pdfService.GenerateInvoicePdfAsync(ctx, CancellationToken.None);

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
        Assert.InRange(bytes.Length, 2000, 10_000_000);
        Assert.Equal(0x25, bytes[0]); // PDF %
    }
}
