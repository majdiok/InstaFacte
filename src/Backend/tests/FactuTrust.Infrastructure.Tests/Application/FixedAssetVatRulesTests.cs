using FactuTrust.Application.Features.FixedAssets;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Résolution partagée « TVA capitalisée ? » (plan T2.1) — utilisée par
/// <c>CreateFixedAssetCommandHandler</c>/<c>UpdateFixedAssetCommandHandler</c>,
/// <c>CreateFixedAssetsFromSupplierInvoiceHandler</c> et <c>AccountingService</c>.
/// </summary>
public sealed class FixedAssetVatRulesTests
{
    [Fact]
    public void IsVatCapitalized_VehPassCategoryCode_ReturnsTrue()
    {
        Assert.True(FixedAssetVatRules.IsVatCapitalized("VEH_PASS", "224"));
    }

    [Fact]
    public void IsVatCapitalized_VehPassCategoryCode_CaseInsensitive_ReturnsTrue()
    {
        Assert.True(FixedAssetVatRules.IsVatCapitalized("veh_pass", "224"));
    }

    [Theory]
    [InlineData("2244")]
    [InlineData("22440001")]
    public void IsVatCapitalized_AssetAccountStartsWith2244_ReturnsTrue(string account)
    {
        Assert.True(FixedAssetVatRules.IsVatCapitalized("OTHER", account));
    }

    [Theory]
    [InlineData("VEH_UTIL", "2241")]
    [InlineData("VEH_UTIL", "224")]
    [InlineData("OTHER", "224")]
    [InlineData(null, "228")]
    public void IsVatCapitalized_NonPassengerVehicle_ReturnsFalse(string? categoryCode, string assetAccount)
    {
        Assert.False(FixedAssetVatRules.IsVatCapitalized(categoryCode, assetAccount));
    }

    [Fact]
    public void IsVatCapitalized_NullAssetAccount_AndNonMatchingCategory_ReturnsFalse()
    {
        Assert.False(FixedAssetVatRules.IsVatCapitalized("OTHER", null));
    }
}
