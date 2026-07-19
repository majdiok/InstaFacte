using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Authorization;

public sealed class UserRolePermissionsTests
{
    [Theory]
    [InlineData(UserRole.Administrator)]
    [InlineData(UserRole.Accountant)]
    [InlineData(UserRole.Supervisor)]
    public void CompanyRoles_DoNotInclude_AccountingValidate(UserRole role)
    {
        Assert.DoesNotContain(Permissions.Accounting.Validate, role.GetPermissions());
    }

    [Fact]
    public void FirmAccountantDelegated_StillIncludes_AccountingValidate()
    {
        Assert.Contains(Permissions.Accounting.Validate, DelegatedPermissionCatalog.FirmAccountantDelegated);
    }

    [Fact]
    public void FirmManagerDelegated_StillIncludes_AccountingValidate()
    {
        Assert.Contains(Permissions.Accounting.Validate, DelegatedPermissionCatalog.FirmManagerDelegated);
    }
}
