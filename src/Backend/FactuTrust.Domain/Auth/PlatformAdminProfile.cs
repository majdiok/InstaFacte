using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Auth;

/// <summary>
/// Lot B2 — Profil étendu d'un administrateur plateforme (Master DB).
///
/// Stocke notamment l'état 2FA TOTP (RFC 6238) :
/// <list type="bullet">
///   <item><see cref="MfaSecretEncrypted"/> — secret base32 chiffré via DataProtection.</item>
///   <item><see cref="MfaConfirmedAt"/> — date à partir de laquelle le 2FA est exigé au login. <c>null</c> = pas encore activé.</item>
///   <item><see cref="RecoveryCodesJson"/> — JSON array des codes de récupération hashés (<c>UserManager.PasswordHasher</c>).</item>
///   <item><see cref="FailedMfaAttempts"/> — compteur de codes invalides successifs (lockout après 5).</item>
///   <item><see cref="MfaLockoutUntil"/> — fenêtre de lockout après 5 échecs successifs.</item>
/// </list>
///
/// L'entité est créée à la demande lors du premier setup 2FA (pas pour tous les admins).
/// Suppression cascade depuis <c>ApplicationUser</c> (FK 1-1 sur UserId).
/// </summary>
public sealed class PlatformAdminProfile : Entity
{
    public Guid UserId { get; private set; }

    public string? MfaSecretEncrypted { get; private set; }
    public DateTime? MfaConfirmedAt { get; private set; }
    public string? RecoveryCodesJson { get; private set; }
    public int FailedMfaAttempts { get; private set; }
    public DateTime? MfaLockoutUntil { get; private set; }

    private PlatformAdminProfile() { }

    public static PlatformAdminProfile CreateForUser(Guid userId)
    {
        return new PlatformAdminProfile
        {
            UserId = userId,
            FailedMfaAttempts = 0
        };
    }

    /// <summary>Démarre un setup 2FA : stocke le nouveau secret chiffré (état "non confirmé").</summary>
    public void BeginMfaSetup(string encryptedSecret)
    {
        MfaSecretEncrypted = encryptedSecret;
        MfaConfirmedAt = null;
        RecoveryCodesJson = null;
        FailedMfaAttempts = 0;
        MfaLockoutUntil = null;
    }

    /// <summary>Confirme le setup 2FA : marque comme actif + persiste les codes de récupération.</summary>
    public void ConfirmMfaSetup(string recoveryCodesJson)
    {
        if (string.IsNullOrEmpty(MfaSecretEncrypted))
            throw new InvalidOperationException("Aucun secret en attente de confirmation.");
        MfaConfirmedAt = DateTime.UtcNow;
        RecoveryCodesJson = recoveryCodesJson;
        FailedMfaAttempts = 0;
        MfaLockoutUntil = null;
    }

    /// <summary>Met à jour le set de codes de récupération (après consommation d'un code).</summary>
    public void UpdateRecoveryCodes(string recoveryCodesJson)
    {
        RecoveryCodesJson = recoveryCodesJson;
    }

    /// <summary>Désactive complètement le 2FA (l'utilisateur doit reconfirmer s'il le réactive).</summary>
    public void DisableMfa()
    {
        MfaSecretEncrypted = null;
        MfaConfirmedAt = null;
        RecoveryCodesJson = null;
        FailedMfaAttempts = 0;
        MfaLockoutUntil = null;
    }

    /// <summary>Incrémente le compteur d'échecs et applique un lockout 15 min après 5 échecs.</summary>
    public void RegisterFailedAttempt()
    {
        FailedMfaAttempts++;
        if (FailedMfaAttempts >= 5)
        {
            MfaLockoutUntil = DateTime.UtcNow.AddMinutes(15);
        }
    }

    /// <summary>Réinitialise le compteur d'échecs après un succès TOTP / recovery code.</summary>
    public void ResetFailedAttempts()
    {
        FailedMfaAttempts = 0;
        MfaLockoutUntil = null;
    }

    /// <summary>Indique si le 2FA est actuellement actif et utilisable au login.</summary>
    public bool IsMfaEnabled => MfaSecretEncrypted is not null && MfaConfirmedAt is not null;

    /// <summary>Indique si l'utilisateur est temporairement bloqué pour le 2FA.</summary>
    public bool IsMfaLocked => MfaLockoutUntil is not null && MfaLockoutUntil > DateTime.UtcNow;
}
