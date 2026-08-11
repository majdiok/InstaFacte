using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Configuration;

public sealed class FirmPayrollValidateCostSyncPolicyTests
{
    [Fact]
    public void ShouldSync_when_native_firm_and_auto_import_enabled()
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.IsAccountingFirmDelegatedContext).Returns(false);
        user.SetupGet(u => u.TenantId).Returns(Guid.NewGuid());

        var options = new FirmGovernanceOptions { AutoImportOnPayrollValidate = true };

        Assert.True(FirmPayrollValidateCostSyncPolicy.ShouldSyncAfterValidate(options, user.Object));
    }

    [Fact]
    public void ShouldNotSync_when_delegated_firm_context()
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.IsAccountingFirmDelegatedContext).Returns(true);
        user.SetupGet(u => u.TenantId).Returns(Guid.NewGuid());

        var options = new FirmGovernanceOptions { AutoImportOnPayrollValidate = true };

        Assert.False(FirmPayrollValidateCostSyncPolicy.ShouldSyncAfterValidate(options, user.Object));
    }

    [Fact]
    public void ShouldNotSync_when_auto_import_disabled()
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(u => u.IsAccountingFirmDelegatedContext).Returns(false);
        user.SetupGet(u => u.TenantId).Returns(Guid.NewGuid());

        var options = new FirmGovernanceOptions { AutoImportOnPayrollValidate = false };

        Assert.False(FirmPayrollValidateCostSyncPolicy.ShouldSyncAfterValidate(options, user.Object));
    }
}
