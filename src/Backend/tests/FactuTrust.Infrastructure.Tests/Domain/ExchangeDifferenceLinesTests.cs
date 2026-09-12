using FactuTrust.Domain.Services.Accounting;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Multi-devises, lot 3 : sens de l'écriture qui apure un écart de change.
///
/// <para>
/// Une inversion de sens ne se voit pas — l'écriture reste équilibrée, elle impute simplement une
/// perte là où il y avait un gain, et double l'écart sur le compte lettré au lieu de le solder.
/// D'où ces tests, écrits sur le scénario exact des captures.
/// </para>
/// </summary>
public sealed class ExchangeDifferenceLinesTests
{
    private const string Lettered = "40100001";
    private const string Loss = "655";
    private const string Gain = "756";
    private const string Label = "DIFFERENCE DE CHANGE";

    [Fact]
    public void NegativeGap_DebitsTheLetteredAccountAndCreditsTheAdjustment()
    {
        // Dette de 1 000 EUR à 3,31420 (3 314,200) réglée à 3,30200 (3 302,000) : le compte porte
        // 12,200 de crédit en trop. On le débite, et le gain de change part au crédit du 756.
        var lines = ExchangeDifferenceLines.Build(Lettered, Gain, -12.200m, Label);

        Assert.Equal(2, lines.Count);

        var letteredLine = lines[0];
        Assert.Equal(Lettered, letteredLine.AccountNumber);
        Assert.Equal(12.200m, letteredLine.Debit);
        Assert.Equal(0m, letteredLine.Credit);

        var adjustmentLine = lines[1];
        Assert.Equal(Gain, adjustmentLine.AccountNumber);
        Assert.Equal(0m, adjustmentLine.Debit);
        Assert.Equal(12.200m, adjustmentLine.Credit);
    }

    [Fact]
    public void PositiveGap_CreditsTheLetteredAccountAndDebitsTheAdjustment()
    {
        var lines = ExchangeDifferenceLines.Build(Lettered, Loss, 12.200m, Label);

        Assert.Equal(0m, lines[0].Debit);
        Assert.Equal(12.200m, lines[0].Credit);
        Assert.Equal(12.200m, lines[1].Debit);
        Assert.Equal(0m, lines[1].Credit);
    }

    [Theory]
    [InlineData(12.200)]
    [InlineData(-12.200)]
    [InlineData(0.001)]
    [InlineData(-9999.999)]
    public void TheAdjustmentIsAlwaysBalanced(decimal gap)
    {
        var lines = ExchangeDifferenceLines.Build(Lettered, Loss, gap, Label);

        Assert.Equal(lines.Sum(l => l.Debit), lines.Sum(l => l.Credit));
    }

    [Theory]
    [InlineData(12.200)]
    [InlineData(-12.200)]
    public void AmountsAreNeverNegative(decimal gap)
    {
        var lines = ExchangeDifferenceLines.Build(Lettered, Loss, gap, Label);

        Assert.All(lines, l =>
        {
            Assert.True(l.Debit >= 0);
            Assert.True(l.Credit >= 0);
        });
    }

    [Fact]
    public void TheAdjustmentNeutralisesTheGapOnTheLetteredAccount()
    {
        // Le compte lettré porte un écart de -12,200 (crédit excédentaire). Après ajustement, son
        // solde net doit revenir à zéro — c'est la condition pour que le lettrage soit possible.
        const decimal gap = -12.200m;
        var lines = ExchangeDifferenceLines.Build(Lettered, Gain, gap, Label);

        var letteredLine = lines.Single(l => l.AccountNumber == Lettered);
        var newGap = gap + (letteredLine.Debit - letteredLine.Credit);

        Assert.Equal(0m, newGap);
    }

    [Fact]
    public void TheLabelIsCarriedByBothLines()
    {
        var lines = ExchangeDifferenceLines.Build(Lettered, Loss, 5m, Label);

        Assert.All(lines, l => Assert.Equal(Label, l.Label));
    }
}
