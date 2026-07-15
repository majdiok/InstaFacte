using System.ComponentModel.DataAnnotations;

namespace FactuTrust.Application.DTOs;

/// <summary>Lot B2 — État 2FA d'un utilisateur (consulté côté frontend).</summary>
public sealed record MfaStatusDto
{
    /// <summary><c>true</c> quand le 2FA est actif et exigé au login.</summary>
    public bool IsEnabled { get; init; }
    /// <summary><c>true</c> quand un setup est en cours mais non confirmé.</summary>
    public bool HasPendingSetup { get; init; }
    /// <summary>Date de confirmation du 2FA (si activé).</summary>
    public DateTime? EnabledAt { get; init; }
    /// <summary>Indique si l'utilisateur est actuellement bloqué (5 échecs successifs).</summary>
    public bool IsLocked { get; init; }
    /// <summary>Date de fin du lockout (si applicable).</summary>
    public DateTime? LockoutUntil { get; init; }
    /// <summary>Nombre de recovery codes encore utilisables.</summary>
    public int RemainingRecoveryCodes { get; init; }
}

/// <summary>Lot B2 — Réponse au démarrage du setup 2FA.</summary>
public sealed record MfaSetupDto
{
    /// <summary>Secret TOTP en base32 (à afficher pour saisie manuelle).</summary>
    public string SecretBase32 { get; init; } = null!;
    /// <summary>Data URI du QR code à afficher dans la page de setup.</summary>
    public string QrCodeDataUri { get; init; } = null!;
    /// <summary>URL otpauth:// (pour copier directement dans une app authenticator).</summary>
    public string OtpAuthUri { get; init; } = null!;
}

/// <summary>Lot B2 — Réponse à la confirmation du setup. Contient les recovery codes en clair (one-shot).</summary>
public sealed record MfaConfirmDto
{
    /// <summary>10 codes de récupération en clair. À sauvegarder par l'utilisateur immédiatement.</summary>
    public IReadOnlyList<string> RecoveryCodes { get; init; } = Array.Empty<string>();
    public DateTime EnabledAt { get; init; }
}

/// <summary>Lot B2 — Body POST /api/platform/auth/2fa/confirm</summary>
public sealed record MfaConfirmRequest
{
    [Required, RegularExpression(@"^\d{6}$", ErrorMessage = "Le code doit comporter exactement 6 chiffres.")]
    public string Code { get; init; } = null!;
}

/// <summary>Lot B2 — Body POST /api/platform/auth/2fa/verify (étape 2 du login).</summary>
public sealed record MfaVerifyRequest
{
    /// <summary>Ticket court délivré à l'étape 1 du login (durée 5 min).</summary>
    [Required]
    public string Ticket { get; init; } = null!;
    /// <summary>Code TOTP (6 chiffres) ou code de récupération (8 caractères alphanumériques).</summary>
    [Required, StringLength(16, MinimumLength = 6)]
    public string Code { get; init; } = null!;
}

/// <summary>Lot B2 — Body POST /api/platform/auth/2fa/disable.</summary>
public sealed record MfaDisableRequest
{
    [Required] public string Password { get; init; } = null!;
    [Required, RegularExpression(@"^\d{6}$")] public string Code { get; init; } = null!;
}

/// <summary>
/// Lot B2 — Réponse à la 1re étape du login quand le 2FA est exigé.
/// Le client doit immédiatement appeler <c>/api/platform/auth/2fa/verify</c> avec le ticket + le code.
/// </summary>
public sealed record TwoFactorChallengeDto
{
    public bool RequiresTwoFactor { get; init; } = true;
    public string Ticket { get; init; } = null!;
    public DateTime TicketExpiresAt { get; init; }
}
