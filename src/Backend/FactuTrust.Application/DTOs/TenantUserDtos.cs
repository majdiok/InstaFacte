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
