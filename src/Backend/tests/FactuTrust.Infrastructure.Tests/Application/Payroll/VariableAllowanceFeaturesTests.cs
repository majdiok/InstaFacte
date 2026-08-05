using FactuTrust.Application.Features.Payroll.VariableAllowances;
using FactuTrust.Domain.Entities.Payroll;
using Moq;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

public sealed class VariableAllowanceFeaturesTests
{
    [Fact]
    public async Task CreateVariableAllowance_WhenMonthLocked_ReturnsValidationError()
    {
        var runs = new Mock<IPayrollRunRepository>();
        runs.Setup(r => r.HasValidatedOrClosedRunForMonthAsync(2026, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new CreateVariableAllowanceLineCommandHandler(
            Mock.Of<IPayrollVariableAllowanceRepository>(),
            Mock.Of<IEmployeeRepository>(),
            runs.Object);

        var result = await handler.Handle(new CreateVariableAllowanceLineCommand(new UpsertVariableAllowanceLineDto
        {
            EmployeeId = Guid.NewGuid(),
            Year = 2026,
            Month = 8,
            Label = "Prime",
            Amount = 100m
        }), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("verrouillé", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteVariableAllowance_WhenNotFound_ReturnsNotFound()
    {
        var repo = new Mock<IPayrollVariableAllowanceRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollVariableAllowanceLine?)null);

        var handler = new DeleteVariableAllowanceLineCommandHandler(repo.Object, Mock.Of<IPayrollRunRepository>());

        var result = await handler.Handle(new DeleteVariableAllowanceLineCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("NotFound", result.Error.Code, StringComparison.OrdinalIgnoreCase);
    }
}
