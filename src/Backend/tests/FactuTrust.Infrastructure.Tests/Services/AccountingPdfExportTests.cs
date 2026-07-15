using System.Net.Http;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Services;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class AccountingPdfExportTests
{
    private static PdfService BuildPdfService()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        var templateRegistry = new Mock<IDocumentTemplateRegistry>();
        return new PdfService(httpFactory.Object, templateRegistry.Object);
    }

    [Fact]
    public async Task GenerateVatDeclarationPdf_ProducesNonEmptyPdf()
    {
        var dto = new VatDeclarationDto
        {
            Year = 2026,
            Month = 7,
            CollectedVat19 = 1000m,
            DeductibleVatGoods = 300m,
            VatDue = 700m,
            Currency = "TND",
            MonthlyDeclarationV2Enabled = true,
            Fodec = 50m,
            DroitTimbre = 12m,
            Tcl = 8m,
            WithholdingTax = 40m,
            TotalToPay = 810m
        };

        var bytes = await BuildPdfService().GenerateVatDeclarationPdfAsync(dto, "Ma Société SARL", CancellationToken.None);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 500);
        // En-tête PDF « %PDF ».
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
    }

    [Fact]
    public async Task GenerateNctLiassePdf_ProducesNonEmptyPdf()
    {
        var current = new Dictionary<string, decimal>
        {
            ["221"] = 10000m, ["281"] = -2000m, ["31"] = 3000m, ["411"] = 5000m, ["532"] = 8000m,
            ["101"] = -15000m, ["401"] = -4000m, ["70"] = -20000m, ["601"] = 12000m, ["64"] = 3000m
        };
        var liasse = NctStatementBuilder.Build(2026, current, new Dictionary<string, decimal>(), enabled: true);

        var bytes = await BuildPdfService().GenerateNctLiassePdfAsync(liasse, "Ma Société SARL", CancellationToken.None);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 500);
        Assert.Equal((byte)'%', bytes[0]);
    }
}
