using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

/// <summary>
/// DTO for user registration.
/// </summary>
public sealed record RegisterDto
{
    public string Email { get; init; } = null!;
    public string Password { get; init; } = null!;
    public string ConfirmPassword { get; init; } = null!;
    public string FirstName { get; init; } = null!;
    public string LastName { get; init; } = null!;
    
    // Company information
    public string CompanyName { get; init; } = null!;
    public string Nif { get; init; } = null!;
    public TaxRegime TaxRegime { get; init; }
    
    public string Street { get; init; } = null!;
    public string? StreetLine2 { get; init; }
    public string City { get; init; } = null!;
    public string? PostalCode { get; init; }
    public string Governorate { get; init; } = null!;
    
    public string CompanyEmail { get; init; } = null!;
    public string Phone { get; init; } = null!;
    public string? Website { get; init; }
    
    // Stock configuration
    /// <summary>
    /// Name of the default warehouse created for the company.
    /// If not provided, defaults to "Entrepôt Principal".
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("warehouseName")]
    public string? WarehouseName { get; init; }
}

/// <summary>
/// DTO for user login.
/// </summary>
public sealed record LoginDto
{
    public string Email { get; init; } = null!;
    public string Password { get; init; } = null!;
    public bool RememberMe { get; init; }
}

/// <summary>
/// DTO for 2FA verification.
/// </summary>
public sealed record Verify2FaDto
{
    public string Email { get; init; } = null!;
    public string Code { get; init; } = null!;
}

/// <summary>
/// DTO for authentication response.
/// </summary>
public sealed record AuthResponseDto
{
    public string AccessToken { get; init; } = null!;
    public string RefreshToken { get; init; } = null!;
    public DateTime ExpiresAt { get; init; }
    public UserDto User { get; init; } = null!;
    public bool Requires2Fa { get; init; }
}

/// <summary>
/// DTO for user information.
/// </summary>
public sealed record UserDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = null!;
    public string FirstName { get; init; } = null!;
    public string LastName { get; init; } = null!;
    public string FullName => $"{FirstName} {LastName}";
    /// <summary>
    /// Tenant role. JSON uses global API options: string enum in camelCase (e.g. <c>administrator</c>, <c>salesRep</c>).
    /// The Angular SPA normalizes to PascalCase at login for UI guards.
    /// </summary>
    public UserRole Role { get; init; }
    public string RoleDisplay { get; init; } = null!;
    public Guid TenantId { get; init; }
    public string CompanyName { get; init; } = null!;
    public TenantKind TenantKind { get; init; } = TenantKind.Company;
    public string AccessMode { get; init; } = "native";
    public Guid? ContextTenantId { get; init; }
    public string? ContextCompanyName { get; init; }
    public bool TwoFactorEnabled { get; init; }

    /// <summary>Enabled AppModule enum values (int) for UI.</summary>
    public IReadOnlyList<int> EnabledModuleIds { get; init; } = Array.Empty<int>();

    /// <summary>Effective permission strings (role ∩ modules).</summary>
    public IReadOnlyList<string> EffectivePermissions { get; init; } = Array.Empty<string>();
}

/// <summary>
/// DTO for changing password.
/// </summary>
public sealed record ChangePasswordDto
{
    public string CurrentPassword { get; init; } = null!;
    public string NewPassword { get; init; } = null!;
    public string ConfirmNewPassword { get; init; } = null!;
}

/// <summary>
/// DTO for password reset request.
/// </summary>
public sealed record ForgotPasswordDto
{
    public string Email { get; init; } = null!;
}

/// <summary>
/// DTO for password reset.
/// </summary>
public sealed record ResetPasswordDto
{
    public string Email { get; init; } = null!;
    public string Token { get; init; } = null!;
    public string NewPassword { get; init; } = null!;
    public string ConfirmNewPassword { get; init; } = null!;
}

/// <summary>
/// DTO for refresh token request.
/// </summary>
public sealed record RefreshTokenDto
{
    public string RefreshToken { get; init; } = null!;
}

/// <summary>Authentication response for platform (backoffice) operators.</summary>
public sealed record PlatformAuthResponseDto
{
    public string AccessToken { get; init; } = null!;
    public string RefreshToken { get; init; } = null!;
    public DateTime ExpiresAt { get; init; }
    public PlatformUserDto User { get; init; } = null!;
}

/// <summary>
/// Lot B1 — Renvoie l'identité + rôles + permissions effectives de l'utilisateur courant
/// (consommé par <c>GET /api/platform/auth/me/permissions</c>).
/// </summary>
public sealed record PlatformMePermissionsDto
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = null!;
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
}

/// <summary>Platform operator profile (no tenant context).</summary>
public sealed record PlatformUserDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = null!;
    public string FirstName { get; init; } = null!;
    public string LastName { get; init; } = null!;
    public string FullName => $"{FirstName} {LastName}";

    // ----- Lot B1 : multi-rôles + permissions effectives ---------------------
    /// <summary>Rôles plateforme attribués à cet utilisateur (ex: PlatformAdmin, BillingAdmin).</summary>
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    /// <summary>Liste des permissions effectives (union des rôles), au format <c>platform.&lt;cat&gt;:&lt;action&gt;</c>.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
}
