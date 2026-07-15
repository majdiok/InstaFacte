using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Comptabilité budgétaire — règles du domaine : postes (code/libellé/préfixes),
/// statut d'exercice (validation de l'initial) et lignes budgétaires (bornes).
/// </summary>
public sealed class BudgetDomainTests
{
    // ── BudgetPost ──────────────────────────────────────────────────────────

    [Fact]
    public void BudgetPost_Create_NormalizesCodeAndPrefixes()
    {
        var result = BudgetPost.Create(" 61 ", "  Services extérieurs ", BudgetPostKind.Expense, " 61 ; 62 ", 20);

        Assert.True(result.IsSuccess);
        Assert.Equal("61", result.Value.Code);
        Assert.Equal("Services extérieurs", result.Value.Label);
        Assert.Equal("61;62", result.Value.AccountPrefixes);
        Assert.Equal(new[] { "61", "62" }, result.Value.GetPrefixes());
        Assert.True(result.Value.IsActive);
    }

    [Theory]
    [InlineData("", "Achats", "60")]
    [InlineData("60", "", "60")]
    [InlineData("60", "Achats", "")]
    [InlineData("60", "Achats", "6a")]
    [InlineData("60", "Achats", "123456789")] // > 8 chiffres
    [InlineData("60", "Achats", "60;60")]     // doublon
    public void BudgetPost_Create_InvalidInputs_Fail(string code, string label, string prefixes)
    {
        var result = BudgetPost.Create(code, label, BudgetPostKind.Expense, prefixes);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void BudgetPost_Update_ValidatesPrefixes_AndToggleActive()
    {
        var post = BudgetPost.Create("61", "Services", BudgetPostKind.Expense, "61").Value;

        Assert.True(post.Update("Services extérieurs", BudgetPostKind.Expense, "61;613", 25).IsSuccess);
        Assert.Equal("61;613", post.AccountPrefixes);
        Assert.Equal(25, post.DisplayOrder);
        Assert.True(post.Update("Services", BudgetPostKind.Expense, "xx", 25).IsFailure);

        post.ToggleActive();
        Assert.False(post.IsActive);
        post.ToggleActive();
        Assert.True(post.IsActive);
    }

    // ── BudgetYear ──────────────────────────────────────────────────────────

    [Fact]
    public void BudgetYear_ValidateInitial_SetsStatus_ThenRefusesSecondValidation()
    {
        var year = BudgetYear.Create(2026).Value;
        Assert.Equal(BudgetVersion.Initial, year.EditableVersion);

        var first = year.ValidateInitial("expert@cabinet.tn");
        Assert.True(first.IsSuccess);
        Assert.Equal(BudgetYearStatus.Validated, year.Status);
        Assert.Equal("expert@cabinet.tn", year.ValidatedBy);
        Assert.NotNull(year.ValidatedAt);
        Assert.Equal(BudgetVersion.Revised, year.EditableVersion);

        var second = year.ValidateInitial("autre@cabinet.tn");
        Assert.True(second.IsFailure);
        Assert.Contains("déjà validé", second.Error.Description);
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    public void BudgetYear_Create_InvalidFiscalYear_Fails(int fiscalYear)
    {
        Assert.True(BudgetYear.Create(fiscalYear).IsFailure);
    }

    // ── BudgetLine ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void BudgetLine_Create_InvalidMonth_Fails(int month)
    {
        var result = BudgetLine.Create(Guid.NewGuid(), 2026, BudgetVersion.Initial, month, 100m);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void BudgetLine_Create_NegativeAmount_OrEmptyPost_Fails()
    {
        Assert.True(BudgetLine.Create(Guid.NewGuid(), 2026, BudgetVersion.Initial, 1, -1m).IsFailure);
        Assert.True(BudgetLine.Create(Guid.Empty, 2026, BudgetVersion.Initial, 1, 100m).IsFailure);
    }

    [Fact]
    public void BudgetLine_Create_HappyPath()
    {
        var postId = Guid.NewGuid();
        var result = BudgetLine.Create(postId, 2026, BudgetVersion.Revised, 12, 1500.250m);

        Assert.True(result.IsSuccess);
        Assert.Equal(postId, result.Value.BudgetPostId);
        Assert.Equal(2026, result.Value.FiscalYear);
        Assert.Equal(BudgetVersion.Revised, result.Value.Version);
        Assert.Equal(12, result.Value.Month);
        Assert.Equal(1500.250m, result.Value.Amount);
    }
}
