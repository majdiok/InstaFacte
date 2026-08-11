using FactuTrust.Application.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Features.Payroll.Commands;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Common;
using MediatR;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Authorization;

public sealed class PayrollFirmOperationBehaviorTests
{
    [Fact]
    public async Task Allows_non_exclusive_request_without_guard_check()
    {
        var guard = new Mock<IPayrollFirmOperationGuard>(MockBehavior.Strict);
        var behavior = new PayrollFirmOperationBehavior<CreateEmployeeCommand, Result<Guid>>(guard.Object);
        var called = false;

        var result = await behavior.Handle(
            new CreateEmployeeCommand(new CreateEmployeeDto()),
            () =>
            {
                called = true;
                return Task.FromResult(Result.Success(Guid.NewGuid()));
            },
            CancellationToken.None);

        Assert.True(called);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Blocks_exclusive_request_when_guard_denies()
    {
        var guard = new Mock<IPayrollFirmOperationGuard>();
        guard.Setup(g => g.CanExecuteFirmExclusiveOperationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var behavior = new PayrollFirmOperationBehavior<CreatePayrollRunCommand, Result<Guid>>(guard.Object);

        var result = await behavior.Handle(
            new CreatePayrollRunCommand(new CreatePayrollRunDto { Year = 2026, Month = 8 }),
            () => Task.FromResult(Result.Success(Guid.NewGuid())),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PayrollOperationsAccess.WriteDeniedErrorCode, result.Error.Code);
    }

    [Fact]
    public async Task Allows_exclusive_request_when_guard_permits()
    {
        var guard = new Mock<IPayrollFirmOperationGuard>();
        guard.Setup(g => g.CanExecuteFirmExclusiveOperationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var behavior = new PayrollFirmOperationBehavior<CreatePayrollRunCommand, Result<Guid>>(guard.Object);
        var expectedId = Guid.NewGuid();

        var result = await behavior.Handle(
            new CreatePayrollRunCommand(new CreatePayrollRunDto { Year = 2026, Month = 8 }),
            () => Task.FromResult(Result.Success(expectedId)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedId, result.Value);
    }
}
