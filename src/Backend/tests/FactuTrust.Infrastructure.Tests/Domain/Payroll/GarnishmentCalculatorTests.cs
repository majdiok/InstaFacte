using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class GarnishmentCalculatorTests
{
    private static IReadOnlyList<PayrollGarnishmentBracket> DefaultBrackets() =>
    [
        PayrollGarnishmentBracket.Create(0m, 0.05m),
        PayrollGarnishmentBracket.Create(500m, 0.10m),
        PayrollGarnishmentBracket.Create(1000m, 0.20m),
        PayrollGarnishmentBracket.Create(2000m, 0.33m)
    ];

    [Fact]
    public void ComputeAvailableSeizable_UsesBracketProgression()
    {
        var available = GarnishmentCalculator.ComputeAvailableSeizable(1500m, DefaultBrackets(), hasAlimony: false);
        Assert.True(available > 0m);
        Assert.True(available < 1500m);
    }

    [Fact]
    public void Allocate_RespectsPriorityAndAvailableAmount()
    {
        var requests = new[]
        {
            new GarnishmentCalculator.GarnishmentRequest(Guid.NewGuid(), GarnishmentType.Garnishment, "Saisie 1", 2, DateTime.UtcNow, 500m, null),
            new GarnishmentCalculator.GarnishmentRequest(Guid.NewGuid(), GarnishmentType.Alimony, "Pension", 1, DateTime.UtcNow, 400m, null)
        };

        var allocations = GarnishmentCalculator.Allocate(300m, requests);
        Assert.Equal(2, allocations.Count);
        Assert.True(allocations.First(a => a.Type == GarnishmentType.Alimony).AppliedAmount > 0);
        Assert.True(allocations.Sum(a => a.AppliedAmount) <= 300m);
    }
}

public sealed class LoanScheduleGeneratorTests
{
    [Fact]
    public void Generate_SplitsPrincipalWithLastInstallmentAdjustment()
    {
        var schedule = LoanScheduleGenerator.Generate(1000m, 3, 2026, 1);
        Assert.Equal(3, schedule.Count);
        Assert.Equal(1000m, schedule.Sum(s => s.Amount));
    }
}
