using FactuTrust.Application.Common.Interfaces.Treasury;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Treasury;

/// <summary>
/// Sélection des comptes entrant dans la position de trésorerie.
/// </summary>
/// <remarks>
/// L'exclusion des virements internes (58x) est le point sensible : un transfert caisse → banque
/// transite par ce compte, et l'inclure ferait compter deux fois la même somme dans le solde
/// d'ouverture d'une projection.
/// </remarks>
public sealed class TreasuryAccountRootsTests
{
    [Theory]
    [InlineData("5321", true)]     // banque
    [InlineData("532100", true)]   // sous-compte banque
    [InlineData("5411", true)]     // caisse
    [InlineData("54", true)]
    [InlineData("581", false)]     // virement interne
    [InlineData("58", false)]
    [InlineData("4111", false)]    // client
    [InlineData("607", false)]     // achat
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsTreasuryAccount_SelectsFinancialAccountsExceptInternalTransfers(
        string? accountNumber,
        bool expected)
    {
        Assert.Equal(expected, TreasuryAccountRoots.IsTreasuryAccount(accountNumber));
    }

    [Theory]
    [InlineData("5411", true)]
    [InlineData("54", true)]
    [InlineData("5321", false)]
    [InlineData("581", false)]
    [InlineData(null, false)]
    public void IsCashAccount_DistinguishesCashFromBank(string? accountNumber, bool expected)
    {
        Assert.Equal(expected, TreasuryAccountRoots.IsCashAccount(accountNumber));
    }

    [Fact]
    public void IsTreasuryAccount_IgnoresSurroundingWhitespace()
    {
        Assert.True(TreasuryAccountRoots.IsTreasuryAccount("  5321  "));
        Assert.False(TreasuryAccountRoots.IsTreasuryAccount("  581  "));
    }

    [Fact]
    public void RoundBalance_UsesMillimeRoundingAwayFromZero()
    {
        Assert.Equal(1.235m, TreasuryAccountRoots.RoundBalance(1.2345m));
        Assert.Equal(-1.235m, TreasuryAccountRoots.RoundBalance(-1.2345m));
    }
}
