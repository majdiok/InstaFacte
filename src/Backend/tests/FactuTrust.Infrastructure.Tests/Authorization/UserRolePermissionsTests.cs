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

    #region SalesOrders permissions

    [Theory]
    [InlineData(UserRole.Administrator)]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.SalesManager)]
    [InlineData(UserRole.SalesRep)]
    [InlineData(UserRole.Accountant)]
    public void SalesRoles_CanCreateReadUpdateSalesOrders(UserRole role)
    {
        Assert.Contains(Permissions.SalesOrders.Create, role.GetPermissions());
        Assert.Contains(Permissions.SalesOrders.Read, role.GetPermissions());
        Assert.Contains(Permissions.SalesOrders.Update, role.GetPermissions());
    }

    [Theory]
    [InlineData(UserRole.Administrator)]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.SalesManager)]
    public void SalesManagers_CanDeleteSalesOrders(UserRole role)
    {
        Assert.Contains(Permissions.SalesOrders.Delete, role.GetPermissions());
    }

    [Theory]
    [InlineData(UserRole.SalesRep)]
    [InlineData(UserRole.Accountant)]
    [InlineData(UserRole.Auditor)]
    [InlineData(UserRole.Client)]
    [InlineData(UserRole.Cashier)]
    [InlineData(UserRole.Warehouse)]
    [InlineData(UserRole.Purchaser)]
    [InlineData(UserRole.Developer)]
    public void NonManagerRoles_CannotDeleteSalesOrders(UserRole role)
    {
        Assert.DoesNotContain(Permissions.SalesOrders.Delete, role.GetPermissions());
    }

    [Fact]
    public void Auditor_CanReadSalesOrdersOnly()
    {
        Assert.Contains(Permissions.SalesOrders.Read, UserRole.Auditor.GetPermissions());
        Assert.DoesNotContain(Permissions.SalesOrders.Create, UserRole.Auditor.GetPermissions());
        Assert.DoesNotContain(Permissions.SalesOrders.Update, UserRole.Auditor.GetPermissions());
        Assert.DoesNotContain(Permissions.SalesOrders.Delete, UserRole.Auditor.GetPermissions());
    }

    [Theory]
    [InlineData(UserRole.Client)]
    [InlineData(UserRole.Cashier)]
    [InlineData(UserRole.Warehouse)]
    [InlineData(UserRole.Purchaser)]
    [InlineData(UserRole.Developer)]
    public void NonSalesRoles_CannotAccessSalesOrders(UserRole role)
    {
        Assert.DoesNotContain(Permissions.SalesOrders.Create, role.GetPermissions());
        Assert.DoesNotContain(Permissions.SalesOrders.Read, role.GetPermissions());
        Assert.DoesNotContain(Permissions.SalesOrders.Update, role.GetPermissions());
        Assert.DoesNotContain(Permissions.SalesOrders.Delete, role.GetPermissions());
    }

    #endregion

    [Fact]
    public void FirmAccountant_DoesNotInclude_NativeHonorairesPermissions()
    {
        var permissions = UserRole.FirmAccountant.GetPermissions();

        Assert.DoesNotContain(Permissions.HonorairesInvoices.Read, permissions);
        Assert.DoesNotContain(Permissions.HonorairesInvoices.Create, permissions);
        Assert.DoesNotContain(Permissions.HonorairesQuotes.Read, permissions);
        Assert.DoesNotContain(Permissions.HonorairesPayments.Read, permissions);
    }

    [Fact]
    public void FirmManager_Includes_NativeHonorairesPermissions()
    {
        var permissions = UserRole.FirmManager.GetPermissions();

        Assert.Contains(Permissions.HonorairesInvoices.Read, permissions);
        Assert.Contains(Permissions.HonorairesInvoices.Create, permissions);
        Assert.Contains(Permissions.HonorairesPayments.Read, permissions);
    }
}
