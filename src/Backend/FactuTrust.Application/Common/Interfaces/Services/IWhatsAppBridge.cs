using FactuTrust.Application.Features.Channels.Bridge;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>Instantané d'état du pont WhatsApp (pour la page d'administration).</summary>
/// <param name="State">État courant de la session.</param>
/// <param name="Qr">QR brut à scanner quand <see cref="BridgeState.WaitingQr"/>, sinon null.</param>
/// <param name="LastError">Dernier message d'erreur (auth/lancement), sinon null.</param>
/// <param name="SinceUtc">Depuis quand l'état courant est en vigueur.</param>
public sealed record BridgeStatus(BridgeState State, string? Qr, string? LastError, DateTime SinceUtc);

/// <summary>
/// Pont WhatsApp piloté par l'API : encapsule le processus Node enfant (whatsapp-web.js) et son
/// cycle de vie. Contrat best-effort côté envoi (ne lève jamais). Une seule instance par
/// application (session WhatsApp unique).
/// </summary>
public interface IWhatsAppBridge
{
    /// <summary>État courant du pont (jamais null ; renvoie un état inerte quand désactivé).</summary>
    BridgeStatus Status { get; }

    /// <summary>Envoie un message texte à la discussion donnée. Renvoie false si non prêt/désactivé.</summary>
    Task<bool> SendTextAsync(string chatId, string text, CancellationToken cancellationToken = default);

    /// <summary>Redémarre le processus du pont (relance la session, réémet un QR si nécessaire).</summary>
    Task RestartAsync();

    /// <summary>Déconnecte la session WhatsApp (efface l'authentification → nouveau QR au redémarrage).</summary>
    Task LogoutAsync();
}
