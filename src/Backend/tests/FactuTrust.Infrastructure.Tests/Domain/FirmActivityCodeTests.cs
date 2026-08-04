using FactuTrust.Domain.Entities.FirmGovernance;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class FirmActivityCodeTests
{
    private static readonly Guid FirmId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Create_with_null_default_unit_price_succeeds()
    {
        var result = FirmActivityCode.Create(FirmId, "CONSEIL", "Conseil", FirmActivityCategory.Advisory);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.DefaultUnitPrice);
    }

    [Fact]
    public void Create_with_zero_default_unit_price_succeeds()
    {
        var result = FirmActivityCode.Create(
            FirmId, "CONSEIL", "Conseil", FirmActivityCategory.Advisory, defaultUnitPrice: 0m);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value.DefaultUnitPrice);
    }

    [Fact]
    public void Create_with_positive_default_unit_price_stores_rounded_value()
    {
        var result = FirmActivityCode.Create(
            FirmId, "CONSEIL", "Conseil", FirmActivityCategory.Advisory, defaultUnitPrice: 200.1234m);

        Assert.True(result.IsSuccess);
        Assert.Equal(200.123m, result.Value.DefaultUnitPrice);
    }

    [Fact]
    public void Create_with_negative_default_unit_price_fails()
    {
        var result = FirmActivityCode.Create(
            FirmId, "CONSEIL", "Conseil", FirmActivityCategory.Advisory, defaultUnitPrice: -1m);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.DefaultUnitPrice", result.Error.Code);
    }

    [Fact]
    public void Create_with_price_above_ceiling_fails()
    {
        var result = FirmActivityCode.Create(
            FirmId, "CONSEIL", "Conseil", FirmActivityCategory.Advisory, defaultUnitPrice: 1_000_000_000m);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.DefaultUnitPrice", result.Error.Code);
    }

    [Fact]
    public void Update_replaces_default_unit_price()
    {
        var entity = FirmActivityCode.Create(
            FirmId, "CONSEIL", "Conseil", FirmActivityCategory.Advisory, defaultUnitPrice: 100m).Value;

        var update = entity.Update("Conseil juridique", FirmActivityCategory.Advisory, true, 20, 250.5m);

        Assert.True(update.IsSuccess);
        Assert.Equal(250.5m, entity.DefaultUnitPrice);
        Assert.Equal("Conseil juridique", entity.Label);
    }

    [Fact]
    public void Update_can_clear_default_unit_price()
    {
        var entity = FirmActivityCode.Create(
            FirmId, "CONSEIL", "Conseil", FirmActivityCategory.Advisory, defaultUnitPrice: 100m).Value;

        var update = entity.Update("Conseil", FirmActivityCategory.Advisory, true, 10, null);

        Assert.True(update.IsSuccess);
        Assert.Null(entity.DefaultUnitPrice);
    }

    [Fact]
    public void Update_with_negative_price_fails_and_keeps_previous_value()
    {
        var entity = FirmActivityCode.Create(
            FirmId, "CONSEIL", "Conseil", FirmActivityCategory.Advisory, defaultUnitPrice: 100m).Value;

        var update = entity.Update("Conseil", FirmActivityCategory.Advisory, true, 10, -5m);

        Assert.True(update.IsFailure);
        Assert.Equal(100m, entity.DefaultUnitPrice);
    }
}
