using FactuTrust.Domain.ClientPortal;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.ClientPortal;

public sealed class ClientPortalStaffRulesTests
{
    [Fact]
    public void Client_role_is_not_staff_assignable()
    {
        Assert.False(ClientPortalStaffRules.IsAssignableByStaff(UserRole.Client));
        Assert.True(ClientPortalStaffRules.IsAssignableByStaff(UserRole.Accountant));
        Assert.True(ClientPortalStaffRules.IsAssignableByStaff(UserRole.Administrator));
    }
}
