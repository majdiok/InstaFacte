using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class InvoiceLineCreateCustomTests
{
    [Fact]
    public void CreateCustom_HasNoProductId()
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;
        var invoice = Invoice.Create(InvoiceNumber.Create("FAC", 2026, 10), client, new DateTime(2026, 8, 1)).Value;
        var price = Money.Create(4500m, Money.DefaultCurrency);

        var result = invoice.AddCustomLine("Forfait — Projet test", null, 1m, "Forfait", price, VatRate.Standard);

        Assert.True(result.IsSuccess, result.Error.Description);
        var line = invoice.Lines.Single();
        Assert.Null(line.ProductId);
        Assert.Null(line.Product);
        Assert.Equal("CUSTOM", line.ProductCode);
        Assert.Equal("Forfait — Projet test", line.ProductName);
    }
}
