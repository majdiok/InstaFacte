using System.ComponentModel.DataAnnotations;

namespace FactuTrust.Application.DTOs;

/// <summary>Lot B1 — Liste des administrateurs plateforme (page <c>/admins</c>).</summary>
public sealed record PlatformAdminListItemDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = null!;
    public string FirstName { get; init; } = null!;
    public string LastName { get; init; } = null!;
    public string FullName => $"{FirstName} {LastName}";
    public bool IsActive { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    /// <summary><c>true</c> quand le compte est actuellement verrouillé par échecs successifs.</summary>
    public bool IsLockedOut { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>Réponse agrégée pour la page <c>/admins</c> (KPIs + liste).</summary>
public sealed record PlatformAdminListPageDto
{
    public IReadOnlyList<PlatformAdminListItemDto> Items { get; init; } = Array.Empty<PlatformAdminListItemDto>();
    public int TotalCount { get; init; }
    public int ActiveCount { get; init; }
    public int LockedCount { get; init; }
    public int SuperAdminCount { get; init; }
}

/// <summary>Payload de création d'un admin plateforme (Lot B1 — pas d'invitation email avant Lot C2).</summary>
public sealed record CreatePlatformAdminRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = null!;

    [Required, StringLength(100, MinimumLength = 1)]
    public string FirstName { get; init; } = null!;

    [Required, StringLength(100, MinimumLength = 1)]
    public string LastName { get; init; } = null!;

    /// <summary>
    /// Mot de passe initial — minimum 14 caractères (cf. policy admin Lot B1).
    /// Le compte sera typiquement réinitialisé au premier login (non-implémenté ce lot).
    /// </summary>
    [Required, StringLength(128, MinimumLength = 14)]
    public string InitialPassword { get; init; } = null!;

    /// <summary>Rôle initial — doit faire partie de <c>PlatformRoles.All</c>.</summary>
    [Required]
    public string Role { get; init; } = null!;
}

/// <summary>Payload de changement de rôle d'un admin existant.</summary>
public sealed record ChangePlatformAdminRoleRequest
{
    [Required]
    public string Role { get; init; } = null!;
}

/// <summary>Payload de réinitialisation du mot de passe (cas usage : "le admin a oublié son mot de passe").</summary>
public sealed record ResetPlatformAdminPasswordRequest
{
    [Required, StringLength(128, MinimumLength = 14)]
    public string NewPassword { get; init; } = null!;
}
