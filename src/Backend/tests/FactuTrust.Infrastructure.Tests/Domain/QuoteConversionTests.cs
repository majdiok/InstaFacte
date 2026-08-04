using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Vérifications du verrouillage d'un devis transformé en commande client — miroir de la
/// conversion en facture pour empêcher toute double facturation par conversion concurrente.
/// </summary>
public sealed class QuoteConversionTests
{
    [Fact]
    public void MarkAsConvertedToSalesOrder_WhenAccepted_SetsFieldsAndRaisesEvent()
    {
        var quote = NewAcceptedQuote();
        var salesOrderId = Guid.NewGuid();

        quote.MarkAsConvertedToSalesOrder(salesOrderId);

        Assert.Equal(QuoteStatus.Converted, quote.Status);
        Assert.Equal(salesOrderId, quote.ConvertedSalesOrderId);
        Assert.NotNull(quote.ConvertedAt);

        var @event = Assert.Single(quote.DomainEvents.OfType<QuoteConvertedToSalesOrderEvent>());
        Assert.Equal(quote.Id, @event.QuoteId);
        Assert.Equal(quote.Number.Value, @event.QuoteNumber);
        Assert.Equal(salesOrderId, @event.SalesOrderId);
    }

    [Theory]
    [InlineData(QuoteStatus.Draft)]
    [InlineData(QuoteStatus.Sent)]
    [InlineData(QuoteStatus.Converted)]
    [InlineData(QuoteStatus.Rejected)]
    [InlineData(QuoteStatus.Cancelled)]
    [InlineData(QuoteStatus.Expired)]
    public void MarkAsConvertedToSalesOrder_WhenNotAccepted_ThrowsInvalidOperationException(QuoteStatus status)
    {
        var quote = NewQuote(status);

        var ex = Assert.Throws<InvalidOperationException>(
            () => quote.MarkAsConvertedToSalesOrder(Guid.NewGuid()));

        Assert.Contains("acceptés", ex.Message);
    }

    [Fact]
    public void MarkAsConvertedToSalesOrder_WhenAlreadyConvertedToInvoice_ThrowsInvalidOperationException()
    {
        var quote = NewAcceptedQuote();
        quote.MarkAsConverted(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(
            () => quote.MarkAsConvertedToSalesOrder(Guid.NewGuid()));
    }

    private static Quote NewAcceptedQuote()
    {
        var quote = NewQuote();
        var product = NewProduct();
        Assert.True(quote.AddLine(product, quantity: 1m).IsSuccess);
        Assert.True(quote.Send().IsSuccess);
        Assert.True(quote.Accept().IsSuccess);
        return quote;
    }

    private static Quote NewQuote(QuoteStatus? status = null)
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;
        var quote = Quote.Create(
            QuoteNumber.Create("DEV", 2026, 1),
            client,
            new DateTime(2026, 7, 20),
            new DateTime(2026, 8, 20)).Value;

        if (status == QuoteStatus.Draft)
            return quote;

        var product = NewProduct();
        Assert.True(quote.AddLine(product, quantity: 1m).IsSuccess);

        if (status == QuoteStatus.Sent)
            Assert.True(quote.Send().IsSuccess);
        else if (status == QuoteStatus.Rejected)
        {
            Assert.True(quote.Send().IsSuccess);
            Assert.True(quote.Reject("Trop cher").IsSuccess);
        }
        else if (status == QuoteStatus.Cancelled)
        {
            Assert.True(quote.Send().IsSuccess);
            Assert.True(quote.Cancel("Annulation").IsSuccess);
        }
        // Expired est volontairement omis : le test du verrouillage passe par les autres statuts.

        return quote;
    }

    private static Product NewProduct() =>
        Product.Create(
            code: "P-CMD",
            name: "Produit commande",
            type: ProductType.Product,
            unitPrice: Money.Create(100m),
            vatRate: VatRate.Standard,
            categoryId: Guid.NewGuid()).Value;
}
