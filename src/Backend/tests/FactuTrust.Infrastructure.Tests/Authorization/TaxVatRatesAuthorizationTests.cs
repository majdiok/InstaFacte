using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Authorization;

/// <summary>
/// Guards the permission model for GET /api/tax/vat-rates (AccountingRead, not SettingsRead).
/// </summary>
public sealed class TaxVatRatesAuthorizationTests
{
    [Fact]
    public void FirmAccountantDelegated_HasAccountingRead_WithoutSettingsRead()
    {
        var perms = DelegatedPermissionCatalog.FirmAccountantDelegated;

        Assert.Contains(Permissions.Accounting.Read, perms);
        Assert.DoesNotContain(Permissions.Settings.Read, perms);
    }

    [Fact]
    public void FirmManagerDelegated_HasAccountingRead_WithoutSettingsRead()
    {
        var perms = DelegatedPermissionCatalog.FirmManagerDelegated;

        Assert.Contains(Permissions.Accounting.Read, perms);
        Assert.DoesNotContain(Permissions.Settings.Read, perms);
    }

    [Theory]
    [InlineData(UserRole.Administrator)]
    [InlineData(UserRole.Accountant)]
    public void TenantAccountingRoles_HaveAccountingRead(UserRole role)
    {
        Assert.Contains(Permissions.Accounting.Read, role.GetPermissions());
    }
}
