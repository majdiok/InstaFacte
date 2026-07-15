using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>Route master active : identité externe de canal → tenant/utilisateur internes.</summary>
public sealed record ChannelRouteInfo(Guid TenantId, Guid UserId);

/// <summary>Code de liaison fraîchement émis (seul moment où le code circule en clair).</summary>
public sealed record ChannelLinkCodeIssue(string Code, DateTime ExpiresAtUtc);

/// <summary>État de liaison d'un utilisateur pour un canal (numéro masqué côté affichage).</summary>
public sealed record ChannelLinkStatus(bool Linked, string? ExternalUserIdMasked, DateTime? VerifiedAtUtc);

/// <summary>Résultat de la consommation d'un code « LIER » (message générique, sans fuite).</summary>
public sealed record ChannelLinkResult(bool Success);

/// <summary>Résultat de l'enregistrement d'idempotence d'un message entrant.</summary>
public enum ChannelInboundRegistration
{
    /// <summary>Premier traitement : enregistré, le pipeline peut continuer.</summary>
    Registered = 0,

    /// <summary>Message déjà traité (re-livraison / doublon) : court-circuiter sans réponse.</summary>
    Duplicate = 1,

    /// <summary>La route master pointe un lien tenant absent/inactif : traiter comme non lié.</summary>
    LinkMissing = 2
}

/// <summary>
/// Gestion des liaisons d'identité de canal (WhatsApp…). Écrit dans les deux bases : la table
/// tenant (<c>ChannelIdentityLinks</c>/<c>ChannelLinkCodes</c>, source de vérité) et l'index de
/// routage master (<c>ChannelExternalRoutes</c>/<c>ChannelLinkCodePointers</c>) — ordre
/// tenant → master avec compensation best-effort (pas de transaction distribuée).
/// </summary>
public interface IChannelLinkService
{
    /// <summary>
    /// Génère un code de liaison éphémère pour l'utilisateur (invalide les codes en attente).
    /// Le code en clair n'est renvoyé qu'ici ; seul son hash SHA-256 est persisté.
    /// </summary>
    Task<ChannelLinkCodeIssue> GenerateLinkCodeAsync(
        Guid tenantId, Guid userId, ChannelType channelType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consomme un code reçu par « LIER &lt;code&gt; » : pointeur master → re-validation tenant →
    /// création/re-liaison du <c>ChannelIdentityLink</c> + upsert de la route master.
    /// Échec toujours générique (pas de distinction inexistant/expiré/verrouillé).
    /// </summary>
    Task<ChannelLinkResult> TryConsumeLinkCodeAsync(
        ChannelType channelType, string rawCode, string externalUserId, string externalChatId,
        CancellationToken cancellationToken = default);

    /// <summary>Route master active pour une identité externe, ou null.</summary>
    Task<ChannelRouteInfo?> FindActiveRouteAsync(
        ChannelType channelType, string externalUserId, CancellationToken cancellationToken = default);

    /// <summary>« DELIER » depuis le canal : désactive lien tenant + route master. False si non lié.</summary>
    Task<bool> UnlinkByExternalUserAsync(
        ChannelType channelType, string externalUserId, CancellationToken cancellationToken = default);

    /// <summary>Déliaison depuis l'UI web (utilisateur authentifié). False si non lié.</summary>
    Task<bool> UnlinkByUserAsync(
        Guid tenantId, Guid userId, ChannelType channelType, CancellationToken cancellationToken = default);

    /// <summary>État de liaison pour la page Paramètres → WhatsApp.</summary>
    Task<ChannelLinkStatus> GetLinkStatusAsync(
        Guid tenantId, Guid userId, ChannelType channelType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotence + re-validation du lien tenant en un aller : enregistre le message entrant
    /// (unique par canal + id externe) APRÈS avoir vérifié que le lien tenant est actif et
    /// cohérent avec la route master.
    /// </summary>
    Task<ChannelInboundRegistration> TryRegisterInboundMessageAsync(
        Guid tenantId, ChannelType channelType, string externalMessageId, string externalUserId,
        Guid userId, string traceId, CancellationToken cancellationToken = default);

    /// <summary>Horodate la dernière activité du lien tenant (après un traitement réussi).</summary>
    Task TouchLastSeenAsync(
        Guid tenantId, ChannelType channelType, string externalUserId, CancellationToken cancellationToken = default);
}
