using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Configuration;

public sealed class FirmGovernanceNativeAccessTests
{
    [Fact]
    public void Internal_payroll_disabled_when_flag_off()
    {
        var options = new FirmGovernanceOptions { Enabled = true, EnableFirmInternalPayroll = false };
        Assert.False(FirmGovernanceNativeAccess.IsInternalPayrollEnabled(options));
        Assert.DoesNotContain((int)AppModule.Payroll, FirmGovernanceNativeAccess.BuildNativeFirmModuleIds(options));
    }

    [Fact]
    public void Internal_payroll_enabled_adds_payroll_module_and_permissions()
    {
        var options = new FirmGovernanceOptions { Enabled = true, EnableFirmInternalPayroll = true };
        Assert.True(FirmGovernanceNativeAccess.IsInternalPayrollEnabled(options));
        Assert.Contains((int)AppModule.Payroll, FirmGovernanceNativeAccess.BuildNativeFirmModuleIds(options));

        var perms = FirmGovernanceNativeAccess.AugmentNativeFirmPermissions(
            DelegatedPermissionCatalog.FirmNativePermissions,
            UserRole.FirmManager,
            options);
        Assert.Contains(Permissions.Payroll.ManageEmployees, perms);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void Should_auto_provision_payroll_on_collaborator_create_requires_both_flags(
        bool enabled,
        bool internalPayroll,
        bool autoProvision)
    {
        var options = new FirmGovernanceOptions
        {
            Enabled = enabled,
            EnableFirmInternalPayroll = internalPayroll,
            AutoProvisionPayrollOnCollaboratorCreate = autoProvision
        };

        var expected = enabled && internalPayroll && autoProvision;
        Assert.Equal(expected, FirmGovernanceNativeAccess.ShouldAutoProvisionPayrollOnCollaboratorCreate(options));
    }
}
