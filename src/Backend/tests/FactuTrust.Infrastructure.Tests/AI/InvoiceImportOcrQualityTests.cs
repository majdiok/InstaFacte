using FactuTrust.Application.Features.AI;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class InvoiceImportOcrQualityTests
{
    [Fact]
    public void Score_EmptyText_ReturnsZero()
    {
        Assert.Equal(0, InvoiceImportOcrQuality.Score(null));
        Assert.Equal(0, InvoiceImportOcrQuality.Score("   "));
    }

    [Fact]
    public void IsSufficient_RichInvoiceText_ReturnsTrue()
    {
        const string text = """
            BON DE LIVRAISON N 00396
            Client Test
            Plaque 4 feux Electrique Focus
            Total 650.000 TND
            """;
        Assert.True(InvoiceImportOcrQuality.IsSufficient(text, 80, 0.35));
    }

    [Fact]
    public void IsSufficient_ShortGarbage_ReturnsFalse()
    {
        Assert.False(InvoiceImportOcrQuality.IsSufficient("abc", 80, 0.35));
    }
}
