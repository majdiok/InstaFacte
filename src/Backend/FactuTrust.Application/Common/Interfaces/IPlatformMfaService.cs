using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Lot B2 — Service 2FA TOTP pour les administrateurs plateforme.
///
/// Workflow :
/// <list type="number">
///   <item><see cref="StartSetupAsync"/> → génère un secret + QR data URI ; non confirmé.</item>
///   <item>L'utilisateur scanne le QR avec son authenticator (Google Authenticator, Authy, 1Password…).</item>
///   <item><see cref="ConfirmSetupAsync"/> → vérifie le code TOTP courant + persiste 10 recovery codes.</item>
///   <item>Au prochain login : <see cref="VerifyAsync"/> ou <see cref="VerifyRecoveryCodeAsync"/>.</item>
///   <item><see cref="DisableAsync"/> → demande mot de passe + code TOTP + désactive le 2FA.</item>
/// </list>
/// </summary>
public interface IPlatformMfaService
{
    /// <summary>Renvoie l'état 2FA de l'utilisateur (peut être consulté n'importe quand).</summary>
    Task<MfaStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Démarre un nouveau setup 2FA : génère un nouveau secret et le persiste (non confirmé).
    /// Si un setup ou un 2FA actif existe déjà, ils sont écrasés (l'utilisateur reconfirme).
    /// </summary>
    Task<MfaSetupDto> StartSetupAsync(
        Guid userId,
        string userEmail,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirme le setup en vérifiant le code TOTP courant. Renvoie 10 recovery codes
    /// **en clair** (non hashés) — c'est la SEULE occasion de les voir.
    /// </summary>
    Task<Result<MfaConfirmDto>> ConfirmSetupAsync(
        Guid userId,
        string totpCode,
        CancellationToken cancellationToken = default);

    /// <summary>Vérifie un code TOTP courant pour un utilisateur ayant le 2FA activé.</summary>
    Task<Result> VerifyAsync(
        Guid userId,
        string totpCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Vérifie un code de récupération. Si valide, le code est immédiatement consommé
    /// (retiré de la liste des recovery codes utilisables).
    /// </summary>
    Task<Result> VerifyRecoveryCodeAsync(
        Guid userId,
        string recoveryCode,
        CancellationToken cancellationToken = default);

    /// <summary>Désactive le 2FA après vérification du mot de passe + code TOTP.</summary>
    Task<Result> DisableAsync(
        Guid userId,
        string password,
        string totpCode,
        CancellationToken cancellationToken = default);
}
