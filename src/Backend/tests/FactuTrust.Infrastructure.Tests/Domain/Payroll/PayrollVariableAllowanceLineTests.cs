using FactuTrust.Domain.Entities.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class PayrollVariableAllowanceLineTests
{
    [Fact]
    public void Create_ValidInput_Succeeds()
    {
        var result = PayrollVariableAllowanceLine.Create(
            Guid.NewGuid(), 2026, 8, "Prime de rendement", 150m, taxable: true, subjectToCnss: true);

        Assert.True(result.IsSuccess);
        Assert.Equal("Prime de rendement", result.Value.Label);
        Assert.Equal(150m, result.Value.Amount);
    }

    [Fact]
    public void Create_EmptyLabel_Fails()
    {
        var result = PayrollVariableAllowanceLine.Create(
            Guid.NewGuid(), 2026, 8, "  ", 100m, true, true);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_NonPositiveAmount_Fails()
    {
        var result = PayrollVariableAllowanceLine.Create(
            Guid.NewGuid(), 2026, 8, "Prime", 0m, true, true);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Update_ValidInput_UpdatesFields()
    {
        var line = PayrollVariableAllowanceLine.Create(
            Guid.NewGuid(), 2026, 8, "Prime A", 100m, true, true).Value;

        var update = line.Update("Prime B", 200m, false, false);

        Assert.True(update.IsSuccess);
        Assert.Equal("Prime B", line.Label);
        Assert.Equal(200m, line.Amount);
        Assert.False(line.Taxable);
        Assert.False(line.SubjectToCnss);
    }
}
