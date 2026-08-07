using FactuTrust.Application.Features.Accounting;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Filet de sécurité : ces numéros de comptes gouvernent la comptabilisation des factures.
/// Les modifier change silencieusement des écritures déjà en production — la valeur littérale
/// est donc figée ici, indépendamment de la constante.
/// </summary>
public sealed class TunisianPostingAccountsTests
{
    [Theory]
    [InlineData("4111", nameof(TunisianPostingAccounts.Client))]
    [InlineData("4011", nameof(TunisianPostingAccounts.Supplier))]
    [InlineData("707", nameof(TunisianPostingAccounts.SalesOfGoods))]
    [InlineData("705", nameof(TunisianPostingAccounts.SalesOfServices))]
    [InlineData("607", nameof(TunisianPostingAccounts.PurchasesOfGoods))]
    [InlineData("218", nameof(TunisianPostingAccounts.DefaultFixedAsset))]
    [InlineData("436711", nameof(TunisianPostingAccounts.VatCollected))]
    [InlineData("43666", nameof(TunisianPostingAccounts.VatDeductibleGoods))]
    [InlineData("43662", nameof(TunisianPostingAccounts.VatDeductibleFixedAssets))]
    [InlineData("4477", nameof(TunisianPostingAccounts.Fodec))]
    [InlineData("4478", nameof(TunisianPostingAccounts.FiscalStampOnSale))]
    [InlineData("6371", nameof(TunisianPostingAccounts.FiscalStampOnPurchase))]
    public void Account_KeepsItsHistoricalNumber(string expected, string constantName)
    {
        var actual = typeof(TunisianPostingAccounts)
            .GetField(constantName)!
            .GetRawConstantValue() as string;

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void JournalCodes_KeepTheirHistoricalValues()
    {
        Assert.Equal("JV", TunisianPostingAccounts.SalesJournalCode);
        Assert.Equal("JA", TunisianPostingAccounts.PurchaseJournalCode);
    }

    [Fact]
    public void FiscalStamp_UsesADifferentAccountOnSaleAndOnPurchase()
    {
        // Vente : timbre collecté pour l'État (dette, classe 4).
        // Achat : timbre supporté par l'entreprise (charge, classe 6).
        // L'asymétrie est volontaire — ce test empêche une « harmonisation » accidentelle.
        Assert.NotEqual(
            TunisianPostingAccounts.FiscalStampOnSale,
            TunisianPostingAccounts.FiscalStampOnPurchase);
        Assert.StartsWith("4", TunisianPostingAccounts.FiscalStampOnSale);
        Assert.StartsWith("6", TunisianPostingAccounts.FiscalStampOnPurchase);
    }
}
