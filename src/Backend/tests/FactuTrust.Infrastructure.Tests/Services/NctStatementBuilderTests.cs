using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class NctStatementBuilderTests
{
    // Balance générale équilibrée (net = débit − crédit ; somme des nets = 0 par la partie double).
    private static Dictionary<string, decimal> BalancedTrialBalance() => new()
    {
        ["221"] = 10000m,   // immobilisation corporelle (débit)
        ["281"] = -2000m,   // amortissement (crédit)
        ["31"] = 3000m,     // stock (débit)
        ["411"] = 5000m,    // client (débit)
        ["532"] = 8000m,    // banque (débit)
        ["101"] = -15000m,  // capital (crédit)
        ["401"] = -4000m,   // fournisseur (crédit)
        ["70"] = -20000m,   // ventes (crédit)
        ["601"] = 12000m,   // achats (débit)
        ["64"] = 3000m      // charges de personnel (débit)
    };

    [Fact]
    public void Build_BalanceSheet_IsBalanced_ActifEqualsPassif()
    {
        var cur = BalancedTrialBalance();
        var prev = new Dictionary<string, decimal>();

        var liasse = NctStatementBuilder.Build(2026, cur, prev, enabled: true);

        Assert.True(liasse.BalanceSheet.IsBalanced);
        Assert.Equal(24000m, liasse.BalanceSheet.TotalAssets);
        Assert.Equal(liasse.BalanceSheet.TotalAssets, liasse.BalanceSheet.TotalEquityAndLiabilities);
    }

    [Fact]
    public void Build_IncomeStatement_NetResult_ReconcilesRevenueMinusExpenses()
    {
        var cur = BalancedTrialBalance();
        var prev = new Dictionary<string, decimal>();

        var liasse = NctStatementBuilder.Build(2026, cur, prev, enabled: true);

        // Produits 20000 − charges 15000 = 5000.
        Assert.Equal(5000m, liasse.IncomeStatement.NetResult);
        Assert.Equal(5000m, liasse.IncomeStatement.OperatingResult);
    }

    [Fact]
    public void Build_ResultInEquity_EqualsIncomeNetResult()
    {
        var cur = BalancedTrialBalance();
        var prev = new Dictionary<string, decimal>();

        var liasse = NctStatementBuilder.Build(2026, cur, prev, enabled: true);

        var resultLine = liasse.BalanceSheet.EquityAndLiabilities.First(l => l.Code == "CP4");
        Assert.Equal(liasse.IncomeStatement.NetResult, resultLine.Amount);
    }

    [Fact]
    public void Build_CashFlow_EndsAtActualClosingCash()
    {
        var cur = BalancedTrialBalance();
        var prev = new Dictionary<string, decimal> { ["532"] = 3000m };

        var liasse = NctStatementBuilder.Build(2026, cur, prev, enabled: true);

        Assert.Equal(8000m, liasse.CashFlow.ClosingCash);
        Assert.Equal(3000m, liasse.CashFlow.OpeningCash);
        Assert.Equal(5000m, liasse.CashFlow.NetChange); // 8000 - 3000
    }

    [Fact]
    public void Build_PropagatesEnabledFlag()
    {
        var liasse = NctStatementBuilder.Build(2026, BalancedTrialBalance(), new Dictionary<string, decimal>(), enabled: false);
        Assert.False(liasse.NctStatementsEnabled);
    }

    [Fact]
    public void Build_ProducesMultipleNotes_WithMethodsAndImmobilisations()
    {
        var liasse = NctStatementBuilder.Build(2026, BalancedTrialBalance(), new Dictionary<string, decimal>(), enabled: true);

        Assert.True(liasse.Notes.Count >= 5);
        Assert.Contains(liasse.Notes, n => n.Title.Contains("Méthodes comptables") && !string.IsNullOrWhiteSpace(n.Description));
        Assert.Contains(liasse.Notes, n => n.Title.Contains("Immobilisations"));
        Assert.Contains(liasse.Notes, n => n.Title.Contains("Créances et dettes"));
    }
}
