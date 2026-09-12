using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Multi-devises, lot 0 : la comptabilité est tenue en devise fonctionnelle.
///
/// <para>
/// <see cref="JournalEntryLine.DebitAmount"/> et <see cref="JournalEntryLine.CreditAmount"/> sont
/// des <see cref="Money"/> dont toute la restitution (grand livre, balance, bilan, compte de
/// résultat, états NCT, balance âgée, FEC) somme <c>.Amount</c> <b>sans convertir</b>. Une écriture
/// valorisée en euros y serait donc additionnée à des dinars.
/// </para>
/// <para>
/// La génération automatique propage déjà la devise du document source
/// (<c>AccountingService</c> : <c>var currency = invoice.TotalAmount.Currency;</c>) et l'assistant
/// de facturation accepte déjà EUR et USD — le chemin était donc réellement atteignable. Tant
/// qu'aucune table de taux n'existe, la pièce est refusée explicitement.
/// </para>
/// </summary>
public sealed class JournalEntryFunctionalCurrencyTests
{
    private static IReadOnlyList<JournalLineInput> BalancedLines() => new[]
    {
        new JournalLineInput("4111", "Client", 100m, 0, null, ThirdPartyKind.None),
        new JournalLineInput("707", "Vente", 0, 100m, null, ThirdPartyKind.None)
    };

    private static Result<JournalEntry> CreateWith(string currency) =>
        JournalEntry.Create(1, "JV", new DateTime(2026, 5, 10), "Vente", Guid.NewGuid(),
            false, "Manual", null, BalancedLines(), currency: currency);

    [Fact]
    public void Create_WithFunctionalCurrency_Succeeds()
    {
        var result = CreateWith(Money.DefaultCurrency);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value.Lines, l =>
        {
            Assert.Equal(Money.DefaultCurrency, l.DebitAmount.Currency);
            Assert.Equal(Money.DefaultCurrency, l.CreditAmount.Currency);
        });
    }

    [Fact]
    public void Create_WithDefaultArgument_KeepsFunctionalCurrency()
    {
        var result = JournalEntry.Create(1, "JV", new DateTime(2026, 5, 10), "Vente", Guid.NewGuid(),
            false, "Manual", null, BalancedLines());

        Assert.True(result.IsSuccess);
        Assert.Equal(Money.DefaultCurrency, result.Value.Lines.First().DebitAmount.Currency);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("USD")]
    [InlineData("eur")]
    [InlineData("  EUR  ")]
    public void Create_WithForeignCurrency_IsRefused(string currency)
    {
        var result = CreateWith(currency);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Currency", result.Error.Code);
        Assert.Contains("taux de change", result.Error.Description);
    }

    [Fact]
    public void UpdateDraftLines_WithForeignCurrency_IsRefused()
    {
        var entry = JournalEntry.Create(1, "JV", new DateTime(2026, 5, 10), "Vente", Guid.NewGuid(),
            false, "Manual", null, BalancedLines(),
            initialStatus: JournalEntryStatus.Brouillon).Value;

        var result = entry.UpdateDraftLines("Vente", BalancedLines(), "EUR");

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Currency", result.Error.Code);
    }

    [Fact]
    public void UpdateDraftLines_WithFunctionalCurrency_Succeeds()
    {
        var entry = JournalEntry.Create(1, "JV", new DateTime(2026, 5, 10), "Vente", Guid.NewGuid(),
            false, "Manual", null, BalancedLines(),
            initialStatus: JournalEntryStatus.Brouillon).Value;

        var result = entry.UpdateDraftLines("Vente corrigée", BalancedLines());

        Assert.True(result.IsSuccess);
    }
}
