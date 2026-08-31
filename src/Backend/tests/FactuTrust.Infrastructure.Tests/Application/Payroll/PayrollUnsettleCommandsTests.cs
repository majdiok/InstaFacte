using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Payroll.Commands;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

/// <summary>
/// Outils firm-only de dé-solde ciblé (plan §5.3 / WS-5, R-06) : avance et échéance de prêt.
/// </summary>
public sealed class PayrollUnsettleCommandsTests
{
    private static readonly Guid EmpId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid RunId = Guid.Parse("cccccccc-1111-1111-1111-111111111111");

    /// <summary>UoW factice qui exécute directement la callback (pas de transaction réelle).</summary>
    private sealed class PassthroughTenantUnitOfWork : ITenantUnitOfWork
    {
        public Task<Result> ExecuteAsync(Func<CancellationToken, Task<Result>> action, CancellationToken cancellationToken = default)
            => action(cancellationToken);
        public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> action, CancellationToken cancellationToken = default)
            => action(cancellationToken);
    }

    [Fact]
    public async Task UnsettleAdvance_RoundTrip_MarksAdvanceUnsettled()
    {
        var advance = EmployeeAdvance.Create(EmpId, new DateTime(2026, 6, 1), 100m).Value;
        advance.Settle(RunId);
        Assert.True(advance.IsSettled);

        var advancesRepo = new Mock<IEmployeeAdvanceRepository>();
        advancesRepo.Setup(r => r.GetByIdAsync(advance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(advance);
        advancesRepo.Setup(r => r.UpdateAsync(It.IsAny<EmployeeAdvance>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new UnsettleAdvanceCommandHandler(advancesRepo.Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(new UnsettleAdvanceCommand(advance.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(advance.IsSettled);
        advancesRepo.Verify(r => r.UpdateAsync(advance, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnsettleAdvance_WhenNotSettled_ReturnsValidationFailure()
    {
        var advance = EmployeeAdvance.Create(EmpId, new DateTime(2026, 6, 1), 100m).Value; // non soldée
        var advancesRepo = new Mock<IEmployeeAdvanceRepository>();
        advancesRepo.Setup(r => r.GetByIdAsync(advance.Id, It.IsAny<CancellationToken>())).ReturnsAsync(advance);

        var handler = new UnsettleAdvanceCommandHandler(advancesRepo.Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(new UnsettleAdvanceCommand(advance.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.EmployeeAdvance", result.Error.Code);
        advancesRepo.Verify(r => r.UpdateAsync(It.IsAny<EmployeeAdvance>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnsettleLoanInstallment_RoundTrip_MarksInstallmentUnsettled()
    {
        var loan = EmployeeLoan.Create(EmpId, "PRET-001", 1200m, 12, 2026, 1).Value;
        var installment = loan.Installments.First();
        loan.MarkInstallmentSettled(installment.Id, RunId);
        Assert.True(installment.IsSettled);

        var loansRepo = new Mock<IEmployeeLoanRepository>();
        loansRepo.Setup(r => r.GetByInstallmentIdAsync(installment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(loan);
        loansRepo.Setup(r => r.UpdateAsync(It.IsAny<EmployeeLoan>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new UnsettleLoanInstallmentCommandHandler(loansRepo.Object, new PassthroughTenantUnitOfWork());

        var result = await handler.Handle(new UnsettleLoanInstallmentCommand(installment.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(installment.IsSettled);
        loansRepo.Verify(r => r.UpdateAsync(loan, It.IsAny<CancellationToken>()), Times.Once);
    }
}
