using FactuTrust.Application.Features.FixedAssets;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Validation serveur des comptes d'immobilisation (B5/T8) : format, préfixes NCT et cohérence
/// corporel/incorporel. Ces cas doivent rester en parité stricte avec la validation frontend (T10, C1).
/// </summary>
public sealed class FixedAssetAccountRulesTests
{
    [Theory]
    [InlineData("21")]
    [InlineData("212")]
    [InlineData("218")]
    [InlineData("22")]
    [InlineData("224")]
    [InlineData("2244")]
    [InlineData("271")]
    public void IsValidAssetAccount_AllowedPrefixes_ShouldBeValid(string account) =>
        Assert.True(FixedAssetAccountRules.IsValidAssetAccount(account));

    [Theory]
    [InlineData("281")]
    [InlineData("2812")]
    [InlineData("29")]
    [InlineData("2913")]
    [InlineData("68112")]
    [InlineData("404")]
    public void IsValidAssetAccount_NeverAllowedPrefixes_ShouldBeInvalid(string account) =>
        Assert.False(FixedAssetAccountRules.IsValidAssetAccount(account));

    [Theory]
    [InlineData("281")]
    [InlineData("2812")]
    [InlineData("282")]
    [InlineData("2824")]
    public void IsValidDepreciationAccount_ShouldAcceptOnly281And282Prefixes(string account) =>
        Assert.True(FixedAssetAccountRules.IsValidDepreciationAccount(account));

    [Theory]
    [InlineData("28")]
    [InlineData("283")]
    [InlineData("21")]
    [InlineData("68111")]
    public void IsValidDepreciationAccount_OtherPrefixes_ShouldBeInvalid(string account) =>
        Assert.False(FixedAssetAccountRules.IsValidDepreciationAccount(account));

    [Theory]
    [InlineData("68111")]
    [InlineData("68112")]
    [InlineData("681130")]
    public void IsValidExpenseAccount_ShouldAccept68111And68112AndTolerated6811Prefix(string account) =>
        Assert.True(FixedAssetAccountRules.IsValidExpenseAccount(account));

    [Theory]
    [InlineData("6812")]
    [InlineData("2xx")]
    [InlineData("28")]
    public void IsValidExpenseAccount_InvalidAccounts_ShouldBeRejected(string account) =>
        Assert.False(FixedAssetAccountRules.IsValidExpenseAccount(account));

    [Fact]
    public void IsValidFormat_NonNumeric_ShouldBeRejected() =>
        Assert.False(FixedAssetAccountRules.IsValidFormat("22A"));

    [Fact]
    public void IsValidFormat_TooShort_ShouldBeRejected() =>
        Assert.False(FixedAssetAccountRules.IsValidFormat("2"));

    [Fact]
    public void IsValidFormat_Null_ShouldBeRejected() =>
        Assert.False(FixedAssetAccountRules.IsValidFormat(null));

    [Fact]
    public void Validate_ExpenseAccountIn2xx_ShouldReject()
    {
        var result = FixedAssetAccountRules.Validate("224", "2824", "22");
        Assert.True(result.IsFailure);
        Assert.Equal("Validation.ExpenseAccountNumber", result.Error.Code);
    }

    [Fact]
    public void Validate_VehiclePassengerTriplet_ShouldAccept()
    {
        var result = FixedAssetAccountRules.Validate("224", "2824", "68112");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_IncorporealAssetWithCorporealExpense_ShouldRejectIncoherence()
    {
        // 212 (incorporel, classe 21) ne peut pas être amorti avec une dotation corporelle 68112.
        var result = FixedAssetAccountRules.Validate("212", "2812", "68112");
        Assert.True(result.IsFailure);
        Assert.Equal("Validation.AssetAccountNumber", result.Error.Code);
    }

    [Fact]
    public void Validate_NonNumericAccount_ShouldReject()
    {
        var result = FixedAssetAccountRules.Validate("22A", "2824", "68112");
        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData("221", "2821", "68112")]
    [InlineData("218", "2818", "68111")]
    [InlineData("212", "2812", "68111")]
    [InlineData("222", "2822", "68112")]
    [InlineData("223", "2823", "68112")]
    [InlineData("228", "2828", "68112")]
    [InlineData("2241", "2824", "68112")]
    [InlineData("2244", "2824", "68112")]
    [InlineData("224", "2824", "68112")]
    public void Validate_CanonicalCategoryDefaultsPostT1_ShouldAllBeValid(
        string assetAccount, string depreciationAccount, string expenseAccount)
    {
        var result = FixedAssetAccountRules.Validate(assetAccount, depreciationAccount, expenseAccount);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
    }

    [Fact]
    public void IsValidTriplet_MirrorsValidate()
    {
        Assert.True(FixedAssetAccountRules.IsValidTriplet("224", "2824", "68112"));
        Assert.False(FixedAssetAccountRules.IsValidTriplet("212", "2824", "68112"));
    }
}
