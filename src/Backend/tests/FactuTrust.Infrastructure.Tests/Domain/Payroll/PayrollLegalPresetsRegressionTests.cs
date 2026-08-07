using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class PayrollLegalPresetsRegressionTests
{
    [Fact]
    public void Preset2026_MatchesLegacyCreateDefaults()
    {
        var legacy = PayrollParameterDefaults.CreateDefaults(2026);
        var fromPreset = PayrollLegalPresets.Presets[2026].Materialize(2026);

        Assert.True(legacy.IsSuccess);
        Assert.True(fromPreset.IsSuccess);

        var a = legacy.Value;
        var b = fromPreset.Value;

        Assert.Equal(a.CnssEmployeeRate, b.CnssEmployeeRate);
        Assert.Equal(a.CnssEmployerRate, b.CnssEmployerRate);
        Assert.Equal(a.CssRate, b.CssRate);
        Assert.Equal(a.MonthlySmig, b.MonthlySmig);
        Assert.Equal(a.IrppBrackets.Count, b.IrppBrackets.Count);
    }

    [Theory]
    [InlineData(2020)]
    [InlineData(2024)]
    [InlineData(2026)]
    public void Resolve_ReturnsValidParameters(int year)
    {
        var result = PayrollParameterDefaults.CreateDefaults(year);
        Assert.True(result.IsSuccess);
        Assert.Equal(year, result.Value.FiscalYear);
        Assert.NotEmpty(result.Value.IrppBrackets);
    }
}
