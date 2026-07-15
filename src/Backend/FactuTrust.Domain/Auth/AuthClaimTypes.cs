namespace FactuTrust.Domain.Auth;

/// <summary>JWT / claims for authorization.</summary>
public static class AuthClaimTypes
{
    /// <summary>Effective permission string (e.g. invoices:read), may repeat.</summary>
    public const string Permission = "perm";

    /// <summary>Value <c>modules</c> when permissions are derived from <c>UserModuleGrants</c> (no role fallback for empty <see cref="Permission"/> claims).</summary>
    public const string PermissionSource = "perm_source";

    /// <summary>Home tenant kind: company or accountingFirm.</summary>
    public const string TenantKind = "tenant_kind";

    /// <summary>Active client tenant when firm operates in delegated mode.</summary>
    public const string ContextTenantId = "context_tenant_id";

    /// <summary>native or delegated.</summary>
    public const string AccessMode = "access_mode";

    /// <summary>Display name of active client company in delegated mode.</summary>
    public const string ContextCompanyName = "context_company_name";
}
