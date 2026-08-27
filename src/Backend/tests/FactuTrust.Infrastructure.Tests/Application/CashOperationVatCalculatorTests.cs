using FactuTrust.Application.Features.CashDesk.Services;
using FactuTrust.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Décomposition TTC → HT/TVA (plan §6.2/§9.4) : splits de référence, cas limites « TVA nulle par
/// arrondi » et invariant HT + TVA == TTC.
/// </summary>
public sealed class CashOperationVatCalculatorTests
{
    [Theory]
    [InlineData(1.000, VatRate.Standard, 0.840, 0.160)]
    [InlineData(0.100, VatRate.Standard, 0.084, 0.016)]
    [InlineData(10.505, VatRate.Intermediate, 9.296, 1.209)]
    [InlineData(25.000, VatRate.Reduced, 23.364, 1.636)]
    public void SplitTtc_ReferenceValues_MatchExpected(decimal ttc, VatRate rate, decimal expectedHt, decimal expectedVat)
    {
        var (ht, vat) = CashOperationVatCalculator.SplitTtc(ttc, rate);

        ht.Should().Be(expectedHt);
        vat.Should().Be(expectedVat);
    }

    [Theory]
    [InlineData(1.000)]
    [InlineData(0.100)]
    [InlineData(10.505)]
    [InlineData(25.000)]
    [InlineData(99.999)]
    public void SplitTtc_Exempt_HtEqualsTtcAndVatIsZero(decimal ttc)
    {
        var (ht, vat) = CashOperationVatCalculator.SplitTtc(ttc, VatRate.Exempt);

        ht.Should().Be(ttc);
        vat.Should().Be(0m);
    }

    [Theory]
    [InlineData(0.001, VatRate.Standard, 0.001, 0.000)]
    [InlineData(0.002, VatRate.Standard, 0.002, 0.000)]
    [InlineData(0.004, VatRate.Intermediate, 0.004, 0.000)]
    [InlineData(0.007, VatRate.Reduced, 0.007, 0.000)]
    [InlineData(0.006, VatRate.Standard, 0.005, 0.001)]
    public void SplitTtc_RoundingBoundaries_MatchExpected(decimal ttc, VatRate rate, decimal expectedHt, decimal expectedVat)
    {
        var (ht, vat) = CashOperationVatCalculator.SplitTtc(ttc, rate);

        ht.Should().Be(expectedHt);
        vat.Should().Be(expectedVat);
    }

    [Theory]
    [InlineData(VatRate.Exempt)]
    [InlineData(VatRate.Reduced)]
    [InlineData(VatRate.Intermediate)]
    [InlineData(VatRate.Standard)]
    public void SplitTtc_HtPlusVatAlwaysEqualsTtc_ForSampleAmounts(VatRate rate)
    {
        decimal[] sample =
        {
            0.001m, 0.003m, 0.005m, 0.010m, 0.100m, 0.999m, 1.000m, 1.001m,
            9.999m, 10.505m, 25.000m, 99.999m, 1234.567m
        };

        foreach (var ttc in sample)
        {
            var (ht, vat) = CashOperationVatCalculator.SplitTtc(ttc, rate);
            (ht + vat).Should().Be(ttc, $"HT + TVA doit égaler TTC pour {ttc} @ {rate}");
        }
    }
}
