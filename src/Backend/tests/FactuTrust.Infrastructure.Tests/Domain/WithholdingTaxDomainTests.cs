using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class WithholdingTaxDomainTests
{
    #region WithholdingTaxType

    [Fact]
    public void TaxType_CreateSystem_ShouldSetSystemFlag()
    {
        var type = WithholdingTaxType.CreateSystem(
            "RS2_000001", WithholdingCategory.Honoraires,
            "Honoraires résidents", 3m, "Art. 52-II");

        Assert.True(type.IsSystem);
        Assert.True(type.IsActive);
        Assert.Equal("RS2_000001", type.Code);
        Assert.Equal(3m, type.DefaultRate);
    }

    [Fact]
    public void TaxType_Create_WithValidInput_ShouldSucceed()
    {
        var result = WithholdingTaxType.Create(
            "CUSTOM_001", WithholdingCategory.Autres,
            "Custom type", 5m);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsSystem);
    }

    [Fact]
    public void TaxType_Create_WithEmptyCode_ShouldFail()
    {
        var result = WithholdingTaxType.Create(
            "", WithholdingCategory.Autres, "Label", 5m);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void TaxType_Create_WithInvalidRate_ShouldFail()
    {
        var result = WithholdingTaxType.Create(
            "TEST", WithholdingCategory.Autres, "Label", 150m);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void TaxType_Update_System_ShouldNotChange()
    {
        var type = WithholdingTaxType.CreateSystem(
            "RS2_000001", WithholdingCategory.Honoraires,
            "Original", 3m);

        type.Update("New Label", 5m, null, null, 1);

        Assert.Equal("Original", type.Label);
        Assert.Equal(3m, type.DefaultRate);
    }

    [Fact]
    public void TaxType_Update_NonSystem_ShouldChange()
    {
        var type = WithholdingTaxType.Create(
            "CUSTOM_001", WithholdingCategory.Autres, "Original", 5m).Value;

        type.Update("Updated", 10m, "Art. 52", null, 2);

        Assert.Equal("Updated", type.Label);
        Assert.Equal(10m, type.DefaultRate);
    }

    #endregion
}
