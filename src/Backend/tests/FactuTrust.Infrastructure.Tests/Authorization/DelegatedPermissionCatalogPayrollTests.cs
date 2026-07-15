using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Authorization;

public sealed class DelegatedPermissionCatalogPayrollTests
{
    [Fact]
    public void FirmManagerDelegated_IncludesPayrollReadAndRunButNotManageEmployees()
    {
        var perms = DelegatedPermissionCatalog.FirmManagerDelegated;

        Assert.Contains(Permissions.Payroll.Read, perms);
        Assert.Contains(Permissions.Payroll.RunPayroll, perms);
        Assert.Contains(Permissions.Payroll.Validate, perms);
        Assert.Contains(Permissions.Payroll.Declare, perms);
        Assert.Contains(Permissions.Payroll.Export, perms);
        Assert.Contains(Permissions.Payroll.Settings, perms);
        Assert.DoesNotContain(Permissions.Payroll.ManageEmployees, perms);
    }

    [Fact]
    public void FirmAccountantDelegated_IncludesPayrollReadAndRunButNotManageEmployees()
    {
        var perms = DelegatedPermissionCatalog.FirmAccountantDelegated;

        Assert.Contains(Permissions.Payroll.Read, perms);
        Assert.Contains(Permissions.Payroll.RunPayroll, perms);
        Assert.Contains(Permissions.Payroll.Validate, perms);
        Assert.Contains(Permissions.Payroll.Declare, perms);
        Assert.Contains(Permissions.Payroll.Export, perms);
        Assert.Contains(Permissions.Payroll.Settings, perms);
        Assert.DoesNotContain(Permissions.Payroll.ManageEmployees, perms);
    }

    [Fact]
    public void GetDelegatedPermissions_FirmManager_ExcludesManageEmployees()
    {
        var perms = DelegatedPermissionCatalog.GetDelegatedPermissions(UserRole.FirmManager);
        Assert.DoesNotContain(Permissions.Payroll.ManageEmployees, perms);
    }
}
