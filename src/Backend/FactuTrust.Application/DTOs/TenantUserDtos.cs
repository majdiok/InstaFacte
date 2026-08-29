using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public sealed record UserModuleAccessItemDto
{
    public AppModule Module { get; init; }
    public bool Enabled { get; init; }

    /// <summary>When null, all sub-features (if enabled). When empty, none. Otherwise a subset.</summary>
    public IReadOnlyList<string>? EnabledFeatureKeys { get; init; }
}

public sealed record TenantUserListItemDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = null!;
    public string FirstName { get; init; } = null!;
    public string LastName { get; init; } = null!;
    public UserRole Role { get; init; }
    public string RoleDisplay { get; init; } = null!;
    public bool IsActive { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public IReadOnlyList<int> EnabledModuleIds { get; init; } = Array.Empty<int>();

    /// <summary>Per-module feature keys from grants (for edit UI). <see cref="TenantUserModuleFeaturesDto.FeatureKeys"/> null means all sub-features.</summary>
    public IReadOnlyList<TenantUserModuleFeaturesDto> ModuleFeatures { get; init; } = Array.Empty<TenantUserModuleFeaturesDto>();
}

public sealed record TenantUserModuleFeaturesDto
{
    public AppModule Module { get; init; }

    /// <summary>Grant row enabled flag (edit form toggles).</summary>
    public bool Enabled { get; init; }

    /// <summary><c>null</c> when stored value is null (all sub-features). Otherwise explicit keys (possibly empty).</summary>
    public IReadOnlyList<string>? FeatureKeys { get; init; }
}

public sealed record CreateTenantUserRequest
{
    public string Email { get; init; } = null!;
    public string FirstName { get; init; } = null!;
    public string LastName { get; init; } = null!;
    public string Password { get; init; } = null!;
    public UserRole Role { get; init; }
    public string? PhoneNumber { get; init; }

    /// <summary>When null/empty, no grant rows (full role).</summary>
    public IReadOnlyList<UserModuleAccessItemDto>? ModuleAccess { get; init; }
}

public sealed record BatchCreateTenantUsersRequest
{
    public IReadOnlyList<CreateTenantUserRequest> Users { get; init; } = Array.Empty<CreateTenantUserRequest>();
}

public sealed record UpdateTenantUserRequest
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public UserRole? Role { get; init; }
    public bool? IsActive { get; init; }
    public string? PhoneNumber { get; init; }
    public string? NewPassword { get; init; }
    public IReadOnlyList<UserModuleAccessItemDto>? ModuleAccess { get; init; }
}

/// <summary>
/// One feature entry of the module catalog for a given role (plan §6 Phase 2.1). <see cref="AllowedPermissions"/>
/// is the intersection of the feature's permissions with <c>RoleModuleGrantCeilingExtensions.GetGrantCeiling</c>;
/// <see cref="BasePermissions"/> is the intersection with the role's base permission set;
/// <see cref="IsExtension"/> is true when the feature grants at least one key beyond the role's base.
/// </summary>
public sealed record ModuleCatalogFeatureDto
{
    public string Key { get; init; } = null!;
    public IReadOnlyList<string> BasePermissions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedPermissions { get; init; } = Array.Empty<string>();
    public bool DefaultSelected { get; init; }
    public bool IsExtension { get; init; }
}

/// <summary>
/// One module entry of the module catalog for a given role. <see cref="Grantable"/> is false for the
/// excluded roles (Client/FirmManager/FirmAccountant) and for any module with an empty grant ceiling —
/// a single, unified "empty ceiling" behavior (plan §5.3), never a 400 on read.
/// </summary>
public sealed record ModuleCatalogModuleDto
{
    public AppModule Module { get; init; }
    public string DisplayName { get; init; } = null!;
    public bool Grantable { get; init; }
    public bool DefaultEnabled { get; init; }
    public IReadOnlyList<ModuleCatalogFeatureDto> Features { get; init; } = Array.Empty<ModuleCatalogFeatureDto>();
}

/// <summary>Response of <c>GET /api/tenant-users/module-catalog?role=...</c> (plan §6 Phase 2.1).</summary>
public sealed record ModuleCatalogDto
{
    public UserRole Role { get; init; }
    public IReadOnlyList<ModuleCatalogModuleDto> Modules { get; init; } = Array.Empty<ModuleCatalogModuleDto>();
}
