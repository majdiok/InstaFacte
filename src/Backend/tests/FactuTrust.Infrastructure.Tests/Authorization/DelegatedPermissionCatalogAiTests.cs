using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Authorization;

public sealed class DelegatedPermissionCatalogAiTests
{
    [Theory]
    [InlineData(nameof(DelegatedPermissionCatalog.FirmManagerDelegated))]
    [InlineData(nameof(DelegatedPermissionCatalog.FirmAccountantDelegated))]
    public void Delegated_catalogs_include_ai_chat(string catalogName)
    {
        var perms = catalogName switch
        {
            nameof(DelegatedPermissionCatalog.FirmManagerDelegated) => DelegatedPermissionCatalog.FirmManagerDelegated,
            nameof(DelegatedPermissionCatalog.FirmAccountantDelegated) => DelegatedPermissionCatalog.FirmAccountantDelegated,
            _ => throw new ArgumentOutOfRangeException(nameof(catalogName))
        };

        Assert.Contains(Permissions.AI.Chat, perms);
    }

    [Fact]
    public void FirmNativePermissions_excludes_ai_chat()
    {
        Assert.DoesNotContain(Permissions.AI.Chat, DelegatedPermissionCatalog.FirmNativePermissions);
    }

    [Theory]
    [InlineData(UserRole.FirmManager)]
    [InlineData(UserRole.FirmAccountant)]
    public void GetDelegatedPermissions_includes_ai_chat(UserRole role)
    {
        Assert.Contains(Permissions.AI.Chat, DelegatedPermissionCatalog.GetDelegatedPermissions(role));
    }
}
