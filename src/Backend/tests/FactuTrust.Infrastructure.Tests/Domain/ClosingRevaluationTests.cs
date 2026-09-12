using FactuTrust.Domain.Services.Accounting;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Multi-devises, lot 5 : réévaluation des positions en devise à la clôture.
///
/// <para>
/// Tout se mesure en <b>débit net</b> : une créance donne une position positive, une dette une
/// position négative. C'est ce qui permet de lire le signe de l'écart de la même façon dans les
/// deux cas — et c'est exactement ce que ces tests verrouillent, car une inversion produirait une
/// écriture équilibrée mais fausse, qui passerait tous les contrôles.
/// </para>
/// </summary>
public sealed class ClosingRevaluationTests
{
    private const string Client = "4111";
    private const string Supplier = "4011";
    private const string Gain = "185";
    private const string Loss = "275";
    private const string Label = "ECART DE CONVERSION";

    // Créance de 1 000 EUR entrée à 3,31420 => 3 314,200 TND au bilan.
    private static CurrencyPosition Receivable() => new(Client, "EUR", 1000m, 3314.200m);

    // Dette de 1 000 EUR entrée à 3,31420 => -3 314,200 TND en débit net.
    private static CurrencyPosition Payable() => new(Supplier, "EUR", -1000m, -3314.200m);

    [Fact]
    public void Revalue_RefusesNonPositiveRate()
    {
        Assert.True(ClosingRevaluation.Revalue(Receivable(), 0m).IsFailure);
        Assert.True(ClosingRevaluation.Revalue(Receivable(), -3.3m).IsFailure);
    }

    [Fact]
    public void Receivable_WorthMoreAtClosing_IsALatentGain()
    {
        // Taux de clôture 3,35 > 3,31420 : la créance vaut davantage de dinars.
        var result = ClosingRevaluation.Revalue(Receivable(), 3.35000m).Value;

        Assert.Equal(3350.000m, result.RevaluedFunctional);
        Assert.Equal(35.800m, result.Delta);
        Assert.True(result.IsLatentGain);
    }

    [Fact]
    public void Receivable_WorthLessAtClosing_IsALatentLoss()
    {
        var result = ClosingRevaluation.Revalue(Receivable(), 3.30000m).Value;

        Assert.Equal(-14.200m, result.Delta);
        Assert.True(result.IsLatentLoss);
    }

    [Fact]
    public void Payable_CostingMoreAtClosing_IsALatentLoss()
    {
        // La dette de 1 000 EUR coûte désormais 3 350 TND au lieu de 3 314,200 : on perd.
        // En débit net : -3 350 - (-3 314,200) = -35,800.
        var result = ClosingRevaluation.Revalue(Payable(), 3.35000m).Value;

        Assert.Equal(-35.800m, result.Delta);
        Assert.True(result.IsLatentLoss);
    }

    [Fact]
    public void Payable_CostingLessAtClosing_IsALatentGain()
    {
        var result = ClosingRevaluation.Revalue(Payable(), 3.30000m).Value;

        Assert.Equal(14.200m, result.Delta);
        Assert.True(result.IsLatentGain);
    }

    [Fact]
    public void LatentGain_DebitsTheAccountAndCreditsTheLiabilitySide()
    {
        var revalued = ClosingRevaluation.Revalue(Receivable(), 3.35000m).Value;

        var lines = ClosingRevaluation.BuildLines(new[] { revalued }, Gain, Loss, Label);

        Assert.Equal(2, lines.Count);
        Assert.Equal(Client, lines[0].AccountNumber);
        Assert.Equal(35.800m, lines[0].Debit);
        Assert.Equal(Gain, lines[1].AccountNumber);
        Assert.Equal(35.800m, lines[1].Credit);
    }

    [Fact]
    public void LatentLoss_DebitsTheAssetSideAndCreditsTheAccount()
    {
        var revalued = ClosingRevaluation.Revalue(Receivable(), 3.30000m).Value;

        var lines = ClosingRevaluation.BuildLines(new[] { revalued }, Gain, Loss, Label);

        Assert.Equal(Loss, lines[0].AccountNumber);
        Assert.Equal(14.200m, lines[0].Debit);
        Assert.Equal(Client, lines[1].AccountNumber);
        Assert.Equal(14.200m, lines[1].Credit);
    }

    [Fact]
    public void PositionsWithoutDifference_ProduceNoLine()
    {
        // Réévaluée au taux d'entrée : rien à régulariser.
        var unchanged = ClosingRevaluation.Revalue(Receivable(), 3.31420m).Value;

        Assert.Equal(0m, unchanged.Delta);
        Assert.Empty(ClosingRevaluation.BuildLines(new[] { unchanged }, Gain, Loss, Label));
    }

    [Fact]
    public void TheRevaluationEntryIsAlwaysBalanced()
    {
        var positions = new[]
        {
            ClosingRevaluation.Revalue(Receivable(), 3.35000m).Value,
            ClosingRevaluation.Revalue(Payable(), 3.35000m).Value,
            ClosingRevaluation.Revalue(new CurrencyPosition("4112", "USD", 500m, 1500m), 3.10000m).Value
        };

        var lines = ClosingRevaluation.BuildLines(positions, Gain, Loss, Label);

        Assert.Equal(lines.Sum(l => l.Debit), lines.Sum(l => l.Credit));
        Assert.All(lines, l =>
        {
            Assert.True(l.Debit >= 0);
            Assert.True(l.Credit >= 0);
        });
    }

    [Fact]
    public void OnlyLatentLossesAreProvisioned()
    {
        // Prudence : la perte latente se provisionne, le gain latent ne se constate pas.
        var positions = new[]
        {
            ClosingRevaluation.Revalue(Receivable(), 3.30000m).Value, // perte de 14,200
            ClosingRevaluation.Revalue(Payable(), 3.30000m).Value     // gain de 14,200
        };

        var lines = ClosingRevaluation.BuildProvisionLines(positions, "6865", "1515", Label);

        Assert.Equal(2, lines.Count);
        Assert.Equal("6865", lines[0].AccountNumber);
        Assert.Equal(14.200m, lines[0].Debit);
        Assert.Equal("1515", lines[1].AccountNumber);
        Assert.Equal(14.200m, lines[1].Credit);
    }

    [Fact]
    public void NoLatentLoss_ProducesNoProvision()
    {
        var positions = new[] { ClosingRevaluation.Revalue(Receivable(), 3.35000m).Value };

        Assert.Empty(ClosingRevaluation.BuildProvisionLines(positions, "6865", "1515", Label));
    }
}
