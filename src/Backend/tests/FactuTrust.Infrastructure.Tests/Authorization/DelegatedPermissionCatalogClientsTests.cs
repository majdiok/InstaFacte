using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Authorization;

public sealed class DelegatedPermissionCatalogClientsTests
{
    [Theory]
    [InlineData(nameof(DelegatedPermissionCatalog.FirmManagerDelegated))]
    [InlineData(nameof(DelegatedPermissionCatalog.FirmAccountantDelegated))]
    public void Delegated_catalog_includes_operational_read_permissions(string catalogName)
    {
        var perms = catalogName switch
        {
            nameof(DelegatedPermissionCatalog.FirmManagerDelegated) => DelegatedPermissionCatalog.FirmManagerDelegated,
            nameof(DelegatedPermissionCatalog.FirmAccountantDelegated) => DelegatedPermissionCatalog.FirmAccountantDelegated,
            _ => throw new ArgumentOutOfRangeException(nameof(catalogName))
        };

        Assert.Contains(Permissions.Clients.Read, perms);
        Assert.Contains(Permissions.Suppliers.Read, perms);
        Assert.Contains(Permissions.Quotes.Read, perms);
    }

    [Theory]
    [InlineData(UserRole.FirmManager)]
    [InlineData(UserRole.FirmAccountant)]
    public void GetDelegatedPermissions_includes_clients_read_excludes_client_mutations(UserRole role)
    {
        var perms = DelegatedPermissionCatalog.GetDelegatedPermissions(role);

        Assert.Contains(Permissions.Clients.Read, perms);
        Assert.DoesNotContain(Permissions.Clients.Create, perms);
        Assert.DoesNotContain(Permissions.Clients.Update, perms);
        Assert.DoesNotContain(Permissions.Clients.Delete, perms);
    }
}
