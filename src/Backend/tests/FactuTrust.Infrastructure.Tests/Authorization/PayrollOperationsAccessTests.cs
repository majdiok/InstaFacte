using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Authorization;

public sealed class PayrollOperationsAccessTests
{
    [Theory]
    [InlineData(Permissions.Payroll.RunPayroll, true)]
    [InlineData(Permissions.Payroll.Validate, true)]
    [InlineData(Permissions.Payroll.Settings, true)]
    [InlineData(Permissions.Payroll.Declare, true)]
    [InlineData(Permissions.Payroll.Pay, true)]
    [InlineData(Permissions.Payroll.Read, false)]
    [InlineData(Permissions.Payroll.ManageEmployees, false)]
    [InlineData(Permissions.Payroll.Export, false)]
    public void IsFirmExclusivePermission_ClassifiesCorrectly(string permission, bool expected)
    {
        Assert.Equal(expected, PayrollOperationsAccess.IsFirmExclusivePermission(permission));
    }

    [Fact]
    public void FilterCompanyPermissionsWhenFirmAssigned_RemovesExclusivePermissions()
    {
        var input = new[]
        {
            Permissions.Payroll.Read,
            Permissions.Payroll.RunPayroll,
            Permissions.Payroll.ManageEmployees,
            Permissions.Payroll.Validate,
            Permissions.Payroll.Settings,
            Permissions.Payroll.Export
        };

        var filtered = PayrollOperationsAccess.FilterCompanyPermissionsWhenFirmAssigned(input);

        Assert.Contains(Permissions.Payroll.Read, filtered);
        Assert.Contains(Permissions.Payroll.ManageEmployees, filtered);
        Assert.Contains(Permissions.Payroll.Export, filtered);
        Assert.DoesNotContain(Permissions.Payroll.RunPayroll, filtered);
        Assert.DoesNotContain(Permissions.Payroll.Validate, filtered);
        Assert.DoesNotContain(Permissions.Payroll.Settings, filtered);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void CanExecuteFirmExclusiveOperations_RespectsAssignmentAndContext(
        bool hasActiveFirmAssignment,
        bool isDelegatedFirm,
        bool expected)
    {
        var actual = PayrollOperationsAccess.CanExecuteFirmExclusiveOperations(
            hasActiveFirmAssignment,
            isDelegatedFirm);

        Assert.Equal(expected, actual);
    }
}
