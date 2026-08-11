using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Authorization;

public sealed class PayrollFirmOperationGuardTests
{
    [Fact]
    public async Task CanExecute_returns_true_when_no_firm_assignment()
    {
        var guard = BuildGuard(hasAssignment: false, isDelegatedFirm: false);
        Assert.True(await guard.CanExecuteFirmExclusiveOperationsAsync());
    }

    [Fact]
    public async Task CanExecute_returns_false_for_company_with_assignment()
    {
        var guard = BuildGuard(hasAssignment: true, isDelegatedFirm: false);
        Assert.False(await guard.CanExecuteFirmExclusiveOperationsAsync());
    }

    [Fact]
    public async Task CanExecute_returns_true_for_delegated_firm_with_assignment()
    {
        var guard = BuildGuard(hasAssignment: true, isDelegatedFirm: true);
        Assert.True(await guard.CanExecuteFirmExclusiveOperationsAsync());
    }

    [Fact]
    public async Task RequiresFirmExclusiveExecution_false_when_feature_disabled()
    {
        var guard = BuildGuard(hasAssignment: true, isDelegatedFirm: false, featureEnabled: false);
        Assert.False(await guard.RequiresFirmExclusiveExecutionAsync());
        Assert.True(await guard.CanExecuteFirmExclusiveOperationsAsync());
    }

    private static PayrollFirmOperationGuard BuildGuard(
        bool hasAssignment,
        bool isDelegatedFirm,
        bool featureEnabled = true)
    {
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.TenantId).Returns(Guid.NewGuid());

        var firmAssignment = new Mock<IFirmAssignmentService>();
        firmAssignment
            .Setup(s => s.GetCompanyCurrentAssignmentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasAssignment
                ? new FirmClientAssignmentDto
                {
                    Id = Guid.NewGuid(),
                    CompanyTenantId = Guid.NewGuid(),
                    FirmTenantId = Guid.NewGuid(),
                    CompanyName = "Client",
                    FirmDisplayName = "Cabinet",
                    Status = FirmAssignmentStatus.Active,
                    StatusDisplay = "Active",
                    RequestedAt = DateTime.UtcNow
                }
                : null);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.IsAccountingFirmDelegatedContext).Returns(isDelegatedFirm);

        return new PayrollFirmOperationGuard(
            tenantContext.Object,
            firmAssignment.Object,
            currentUser.Object,
            Options.Create(new PayrollOptions { FirmExclusiveOperations = featureEnabled }));
    }
}
