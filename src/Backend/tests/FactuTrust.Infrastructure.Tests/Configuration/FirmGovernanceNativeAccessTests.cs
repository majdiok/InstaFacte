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

    [Fact]
    public void Firm_agent_permissions_absent_without_options()
    {
        var options = new FirmGovernanceOptions { Enabled = true, EnableFirmInternalPayroll = true };

        // Surcharge historique à 3 arguments : aucune permission d'agent cabinet.
        var perms = FirmGovernanceNativeAccess.AugmentNativeFirmPermissions(
            DelegatedPermissionCatalog.FirmNativePermissions,
            UserRole.FirmManager,
            options);

        Assert.DoesNotContain(Permissions.Firm.AiChat, perms);
        Assert.DoesNotContain(Permissions.Firm.AiRemind, perms);
    }

    [Fact]
    public void Firm_agent_permissions_absent_when_flag_off()
    {
        var perms = FirmGovernanceNativeAccess.AugmentNativeFirmPermissions(
            DelegatedPermissionCatalog.FirmNativePermissions,
            UserRole.FirmManager,
            new FirmGovernanceOptions { Enabled = true },
            new AccountingFirmsOptions { Enabled = true, FirmAgentEnabled = false });

        Assert.DoesNotContain(Permissions.Firm.AiChat, perms);
    }

    [Theory]
    [InlineData(UserRole.FirmManager)]
    [InlineData(UserRole.FirmAccountant)]
    public void Firm_agent_chat_granted_to_both_firm_roles(UserRole role)
    {
        var perms = FirmGovernanceNativeAccess.AugmentNativeFirmPermissions(
            DelegatedPermissionCatalog.FirmNativePermissions,
            role,
            new FirmGovernanceOptions { Enabled = true },
            new AccountingFirmsOptions { Enabled = true, FirmAgentEnabled = true });

        Assert.Contains(Permissions.Firm.AiChat, perms);
    }

    [Fact]
    public void Firm_agent_reminder_reserved_to_manager()
    {
        var accountingFirms = new AccountingFirmsOptions
        {
            Enabled = true,
            FirmAgentEnabled = true,
            FirmAgentReminderToolEnabled = true
        };
        var governance = new FirmGovernanceOptions { Enabled = true };

        var manager = FirmGovernanceNativeAccess.AugmentNativeFirmPermissions(
            DelegatedPermissionCatalog.FirmNativePermissions, UserRole.FirmManager, governance, accountingFirms);
        var accountant = FirmGovernanceNativeAccess.AugmentNativeFirmPermissions(
            DelegatedPermissionCatalog.FirmNativePermissions, UserRole.FirmAccountant, governance, accountingFirms);

        Assert.Contains(Permissions.Firm.AiRemind, manager);
        Assert.DoesNotContain(Permissions.Firm.AiRemind, accountant);
        Assert.Contains(Permissions.Firm.AiChat, accountant);
    }

    /// <summary>
    /// L'agent cabinet n'emprunte jamais la permission de l'assistant tenant : la garde
    /// <c>DelegatedPermissionCatalogAiTests.FirmNativePermissions_excludes_ai_chat</c> doit rester vraie.
    /// </summary>
    [Fact]
    public void Firm_agent_never_grants_tenant_ai_chat()
    {
        var perms = FirmGovernanceNativeAccess.AugmentNativeFirmPermissions(
            DelegatedPermissionCatalog.FirmNativePermissions,
            UserRole.FirmManager,
            new FirmGovernanceOptions { Enabled = true, EnableFirmInternalPayroll = true },
            new AccountingFirmsOptions { Enabled = true, FirmAgentEnabled = true, FirmAgentReminderToolEnabled = true });

        Assert.DoesNotContain(Permissions.AI.Chat, perms);
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
