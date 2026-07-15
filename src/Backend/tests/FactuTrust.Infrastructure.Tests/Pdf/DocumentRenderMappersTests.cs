using FactuTrust.Application.Common.Models;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Services.Templates;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Pdf;

/// <summary>
/// Vérifie la projection Entité -> DocumentRenderModel, en particulier la cohérence HT/TTC
/// (le total de ligne du modèle unifié doit être le HT, pas le TTC).
/// </summary>
public sealed class DocumentRenderMappersTests
{
    private static Invoice BuildInvoice()
    {
        var address = Address.Create("1 rue Test", "Tunis", "Tunis", null, "1000").Value;
        var email = Email.Create("client@test.tn").Value;
        var client = Client.Create("Client Test", ClientType.Individual, address, email).Value;
        var number = InvoiceNumber.Create("FAC", 2026, 1);
        var invoice = Invoice.Create(number, client, new DateTime(2026, 2, 18)).Value;

        // 2 x 150.000 HT @ 19% => HT 300.000, TVA 57.000, TTC ligne 357.000
        Assert.True(invoice.AddCustomLine("Prestation", null, 2, "h", Money.Create(150.000m, "TND"), VatRate.Standard, null).IsSuccess);
        Assert.True(invoice.SetFiscalStampAmount(Money.FromSignedAmount(1m, "TND")).IsSuccess);
        Assert.True(invoice.Validate().IsSuccess);
        return invoice;
    }

    [Fact]
    public void FromInvoice_maps_line_total_as_HT_not_TTC()
    {
        var invoice = BuildInvoice();
        var line = invoice.Lines.First();

        var model = DocumentRenderMappers.FromInvoice(new InvoicePdfContext(invoice, null, null), null, null);

        var mappedLine = Assert.Single(model.Lines);
        Assert.Equal(line.SubTotal.Amount, mappedLine.LineTotalHt); // HT après remise
        Assert.NotEqual(line.Total.Amount, mappedLine.LineTotalHt);  // surtout pas le TTC
        Assert.Equal(150.000m, mappedLine.UnitPriceHt);
        Assert.Equal(19, mappedLine.VatRatePercent);
    }

    [Fact]
    public void FromInvoice_maps_totals_and_vat_breakdown()
    {
        var invoice = BuildInvoice();

        var model = DocumentRenderMappers.FromInvoice(new InvoicePdfContext(invoice, null, null), null, null);

        Assert.Equal("FACTURE", model.TitleLabel);
        Assert.False(model.IsCreditNote);
        Assert.Equal(invoice.SubTotal.Amount, model.SubTotal);
        Assert.Equal(invoice.TotalAmount.Amount, model.Total);
        Assert.Equal(1m, model.FiscalStamp);

        var vat = Assert.Single(model.VatBreakdown);
        Assert.Equal(19, vat.RatePercent);
        Assert.Equal(300.000m, vat.BaseHt);
        Assert.Equal(57.000m, vat.Amount);
    }
}
