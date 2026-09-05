using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Accounting;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Accounting;

/// <summary>
/// Un numéro de compte comporte au plus 8 chiffres, séparateurs non comptés.
/// </summary>
/// <remarks>
/// Le décompte porte sur les <b>chiffres</b> et non sur la longueur de la chaîne : l'overlay métier
/// utilise des sous-comptes pointés (421.1 pour les prêts salariés, 428.1 pour la mutuelle) qu'une
/// règle en caractères pénaliserait sans raison. Le plafond de 8 est celui que
/// <c>BudgetPost.ParsePrefixes</c> applique déjà aux préfixes budgétaires.
/// </remarks>
public sealed class AccountNumberRulesTests
{
    [Theory]
    [InlineData("421.1", 4)]
    [InlineData("428.1", 4)]
    [InlineData("12345678", 8)]
    [InlineData("4259655554", 10)]
    [InlineData("12345678.1", 9)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void DigitCount_IgnoresSeparators(string? accountNumber, int expected)
    {
        Assert.Equal(expected, AccountNumberRules.DigitCount(accountNumber));
    }

    [Theory]
    [InlineData("425")]
    [InlineData("4250001")]
    [InlineData("41100001")]   // 8 chiffres exactement
    [InlineData("421.1")]
    [InlineData("436711")]     // le plus long compte du catalogue NCT 01
    public void IsWellFormed_AcceptsCompliantNumbers(string accountNumber)
    {
        Assert.True(AccountNumberRules.IsWellFormed(accountNumber));
    }

    [Theory]
    [InlineData("4259655554")]  // le compte vu en production : 425 + 7 derniers chiffres du matricule
    [InlineData("4258744456")]
    [InlineData("123456789")]   // 9 chiffres
    [InlineData("12345678.1")]  // 9 chiffres répartis autour d'un point
    [InlineData("8250001")]     // classe 8 hors SCE
    [InlineData("042")]         // ne commence pas par une classe 1 à 7
    [InlineData("A425")]
    [InlineData("425.")]
    [InlineData("")]
    [InlineData(null)]
    public void IsWellFormed_RejectsEverythingElse(string? accountNumber)
    {
        Assert.False(AccountNumberRules.IsWellFormed(accountNumber));
    }

    [Fact]
    public void Validate_NamesTheAccountAndItsDigitCount()
    {
        var result = AccountNumberRules.Validate("4259655554");

        Assert.True(result.IsFailure);
        Assert.Contains("4259655554", result.Error.Description);
        Assert.Contains("10 chiffres", result.Error.Description);
        Assert.Contains("8", result.Error.Description);
    }

    [Fact]
    public void Validate_UsesTheGivenFieldName()
    {
        var result = AccountNumberRules.Validate("123456789", "AuxiliaryAccountNumber");

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.AuxiliaryAccountNumber", result.Error.Code);
    }

    /// <summary>
    /// L'invariant vit dans la fabrique du domaine, pas seulement dans les validators : les chemins
    /// d'auto-création (<c>TryAutoCreateSubAccountAsync</c>,
    /// <c>EnsureEmployeeAuxiliaryAccountsAsync</c>) l'appellent directement, sans FluentValidation.
    /// C'est ce contournement qui avait laissé entrer 4259655554.
    /// </summary>
    [Fact]
    public void ChartOfAccountCreate_RejectsAnOverlongNumber()
    {
        var result = ChartOfAccount.Create(
            "4259655554", "sami samou", 4, "425", AccountNatureType.Credit,
            isSystem: false, accountType: AccountType.Other,
            isAuxiliary: true, affectationAccountNumber: "425");

        Assert.True(result.IsFailure);
        Assert.Contains("10 chiffres", result.Error.Description);
    }

    [Fact]
    public void ChartOfAccountCreate_AcceptsTheAllocatedShape()
    {
        var result = ChartOfAccount.Create(
            "4250001", "sami samou", 4, "425", AccountNatureType.Credit,
            isSystem: false, accountType: AccountType.Other,
            isAuxiliary: true, affectationAccountNumber: "425");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(7, result.Value.Level);
    }

    [Fact]
    public void ChartOfAccountCreate_StillAcceptsDottedOverlayAccounts()
    {
        var result = ChartOfAccount.Create(
            "421.1", "Prêts salariés", 4, "421", AccountNatureType.Debit);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
    }
}
