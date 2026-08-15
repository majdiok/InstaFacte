using FactuTrust.Application.Features.Invoices;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class InvoiceListTypeParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_Blank_MeansAllTypes(string? value)
    {
        var ok = InvoiceListTypeParser.TryParse(value, out var type, out var error);

        Assert.True(ok);
        Assert.Null(type);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("INVOICE", InvoiceType.Standard)]
    [InlineData("invoice", InvoiceType.Standard)]
    [InlineData("Standard", InvoiceType.Standard)]
    [InlineData("0", InvoiceType.Standard)]
    [InlineData("CREDIT_NOTE", InvoiceType.CreditNote)]
    [InlineData("CreditNote", InvoiceType.CreditNote)]
    [InlineData("1", InvoiceType.CreditNote)]
    public void TryParse_KnownAliases_MapsToDomainEnum(string value, InvoiceType expected)
    {
        var ok = InvoiceListTypeParser.TryParse(value, out var type, out var error);

        Assert.True(ok);
        Assert.Equal(expected, type);
        Assert.Null(error);
    }

    [Fact]
    public void TryParse_Unknown_ReturnsValidationError()
    {
        var ok = InvoiceListTypeParser.TryParse("QUOTE", out var type, out var error);

        Assert.False(ok);
        Assert.Null(type);
        Assert.Equal(InvoiceListTypeParser.InvalidTypeMessage, error);
    }
}
