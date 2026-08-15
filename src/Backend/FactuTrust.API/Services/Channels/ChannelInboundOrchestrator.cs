using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.Commands;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.Channels;
using FactuTrust.Application.Features.Channels.DTOs;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Services.Channels;

/// <summary>
/// Coordonne les messages entrants des canaux externes (WhatsApp). Exécuté en job Hangfire
/// fire-and-forget (l'inférence CPU dure des dizaines de secondes — le webhook a déjà répondu 202).
///
/// Séquence : flags → parsing (LIER/DELIER/AIDE/question) → route master → idempotence +
/// re-validation du lien tenant → impersonation (<see cref="ChannelUserContext"/>) + contexte
/// tenant ambiant (AsyncLocal) + garde de migrations → pipeline IA en lecture seule forcée →
/// agrégation du flux → formatage WhatsApp → réponse via la passerelle.
///
/// Invariants : jamais de retry Hangfire (réponses dupliquées) ; jamais de contenu de message ni
/// de code en clair dans les logs ; contextes ambiants toujours nettoyés en finally.
/// </summary>
public sealed class ChannelInboundOrchestrator
{
    private const string ConversationTitlePrefix = "WhatsApp";
    private static readonly TimeSpan ConversationReuseWindow = TimeSpan.FromHours(24);

    private const string OnboardingReply =
        "🔗 Ce numéro WhatsApp n'est pas encore lié à un compte FactuTrust.\n\n" +
        "Dans l'application web : *Paramètres → WhatsApp* → « Générer un code », puis envoyez ici :\n" +
        "LIER VOTRECODE";

    private const string HelpReply =
        "*Assistant FactuTrust sur WhatsApp*\n\n" +
        "• Posez vos questions en français : « Quel est mon chiffre d'affaires ce mois-ci ? », " +
        "« Mes factures impayées ? »…\n" +
        "• LIER <code> — lier ce numéro à votre compte (code à générer dans *Paramètres → WhatsApp*)\n" +
        "• DELIER — supprimer la liaison\n" +
        "• AIDE — afficher ce message\n\n" +
        "ℹ️ Lecture seule : aucune modification de vos données ne peut être faite depuis WhatsApp.";

    private const string LinkSuccessReply =
        "✅ Votre WhatsApp est maintenant lié à votre compte FactuTrust.\n\n" +
        "Posez-moi vos questions (ex. « Quel est mon CA ce mois-ci ? »). " +
        "Envoyez AIDE pour l'aide, DELIER pour délier.";

    private const string LinkFailureReply =
        "❌ Code invalide ou expiré. Générez un nouveau code dans *Paramètres → WhatsApp* " +
        "et renvoyez : LIER VOTRECODE";

    private const string UnlinkSuccessReply =
        "🔓 Ce numéro a été délié de votre compte FactuTrust. " +
        "Vous pouvez le relier à tout moment avec un nouveau code.";

    private const string ProcessingAckReply = "⏳ Je consulte vos données…";

    private const string ErrorReply =
        "⚠️ Une erreur est survenue lors du traitement de votre message. " +
        "Veuillez réessayer dans quelques instants.";

    private const string TimeoutReply =
        "⏱️ Le traitement a pris trop de temps. Veuillez réessayer dans quelques instants.";

    private const string EmptyReply =
        "Je n'ai pas réussi à produire une réponse. Pouvez-vous reformuler votre question ?";

    private const string InactiveUserReply =
        "⛔ Ce compte utilisateur est désactivé. Contactez votre administrateur.";

    private const string NoAiPermissionReply =
        "⛔ Votre compte n'a pas accès à l'assistant IA. Contactez votre administrateur.";

    private readonly ChannelsSettings _settings;
    private readonly IChannelLinkService _linkService;
    private readonly IChannelOutboundSender _outbound;
    private readonly ITenantService _tenantService;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantMigrationGuard _migrationGuard;
    private readonly IEffectivePermissionService _effectivePermissions;
    private readonly MasterDbContext _master;
    private readonly SendChatMessageHandler _chatHandler;
    private readonly IConversationRepository _conversations;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ChannelInboundOrchestrator> _logger;

    public ChannelInboundOrchestrator(
        IOptions<ChannelsSettings> settings,
        IChannelLinkService linkService,
        IChannelOutboundSender outbound,
        ITenantService tenantService,
        ITenantContext tenantContext,
        ITenantMigrationGuard migrationGuard,
        IEffectivePermissionService effectivePermissions,
        MasterDbContext master,
        SendChatMessageHandler chatHandler,
        IConversationRepository conversations,
        TimeProvider timeProvider,
        ILogger<ChannelInboundOrchestrator> logger)
    {
        _settings = settings.Value;
        _linkService = linkService;
        _outbound = outbound;
        _tenantService = tenantService;
        _tenantContext = tenantContext;
        _migrationGuard = migrationGuard;
        _effectivePermissions = effectivePermissions;
        _master = master;
        _chatHandler = chatHandler;
        _conversations = conversations;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Point d'entrée du job Hangfire. <c>Attempts = 0</c> impératif : un retry rejouerait
    /// l'inférence et enverrait des réponses dupliquées — l'idempotence est portée par
    /// <c>ChannelInboundMessageLogs</c>, pas par la file.
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    public async Task ProcessAsync(ChannelInboundMessageDto message, CancellationToken cancellationToken)
    {
        if (!_settings.Enabled || !_settings.WhatsAppEnabled)
            return;
        if (message is null ||
            string.IsNullOrWhiteSpace(message.ExternalUserId) ||
            string.IsNullOrWhiteSpace(message.ExternalChatId))
        {
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(30, _settings.InboundProcessingTimeoutSeconds)));
        var ct = cts.Token;

        try
        {
            var text = (message.Text ?? string.Empty).Trim();
            if (text.Length > _settings.MaxInboundMessageLength)
                text = text[.._settings.MaxInboundMessageLength];

            var command = ChannelCommandParser.Parse(text);
            switch (command.Kind)
            {
                case ChannelCommandKind.Link:
                    await HandleLinkAsync(message, command.Argument, ct);
                    break;
                case ChannelCommandKind.Unlink:
                    await HandleUnlinkAsync(message, ct);
                    break;
                case ChannelCommandKind.Help:
                    await TrySendAsync(message.ExternalChatId, HelpReply, ct);
                    break;
                default:
                    await HandleQuestionAsync(message, text, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Traitement du message canal {ExternalMessageId} en échec.", message.ExternalMessageId);
            await TrySendAsync(message.ExternalChatId, ErrorReply, CancellationToken.None);
        }
    }

    private async Task HandleLinkAsync(ChannelInboundMessageDto message, string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            await TrySendAsync(message.ExternalChatId, LinkFailureReply, ct);
            return;
        }

        var result = await _linkService.TryConsumeLinkCodeAsync(
            ChannelType.WhatsApp, code, message.ExternalUserId, message.ExternalChatId, ct);
        await TrySendAsync(message.ExternalChatId, result.Success ? LinkSuccessReply : LinkFailureReply, ct);
    }

    private async Task HandleUnlinkAsync(ChannelInboundMessageDto message, CancellationToken ct)
    {
        var unlinked = await _linkService.UnlinkByExternalUserAsync(ChannelType.WhatsApp, message.ExternalUserId, ct);
        await TrySendAsync(message.ExternalChatId, unlinked ? UnlinkSuccessReply : OnboardingReply, ct);
    }

    private async Task HandleQuestionAsync(ChannelInboundMessageDto message, string text, CancellationToken ct)
    {
        if (text.Length == 0)
        {
            await TrySendAsync(message.ExternalChatId, HelpReply, ct);
            return;
        }

        var route = await _linkService.FindActiveRouteAsync(ChannelType.WhatsApp, message.ExternalUserId, ct);
        if (route is null)
        {
            await TrySendAsync(message.ExternalChatId, OnboardingReply, ct);
            return;
        }

        // Idempotence + re-validation du lien tenant AVANT toute inférence (les re-livraisons de la
        // passerelle et les doublons concurrents sont court-circuités sans réponse).
        var traceId = Truncate($"wa:{message.ExternalMessageId}", 120);
        var registration = await _linkService.TryRegisterInboundMessageAsync(
            route.TenantId, ChannelType.WhatsApp,
            Truncate(message.ExternalMessageId, 150), Truncate(message.ExternalUserId, 128),
            route.UserId, traceId, ct);
        switch (registration)
        {
            case ChannelInboundRegistration.Duplicate:
                _logger.LogInformation("Message canal {ExternalMessageId} déjà traité — ignoré.", message.ExternalMessageId);
                return;
            case ChannelInboundRegistration.LinkMissing:
                // Route master orpheline (lien tenant retiré) : auto-guérison + onboarding.
                await _linkService.UnlinkByExternalUserAsync(ChannelType.WhatsApp, message.ExternalUserId, CancellationToken.None);
                await TrySendAsync(message.ExternalChatId, OnboardingReply, ct);
                return;
        }

        // Identité impersonnée : utilisateur master + rôle Identity + permissions EFFECTIVES
        // (rôle ∩ droits de modules) — mêmes droits que le JWT web.
        var user = await _master.Users.AsNoTracking()
            .Where(u => u.Id == route.UserId)
            .Select(u => new { u.Id, u.Email, u.TenantId, u.IsActive })
            .FirstOrDefaultAsync(ct);
        if (user is null || !user.IsActive)
        {
            await TrySendAsync(message.ExternalChatId, InactiveUserReply, ct);
            return;
        }

        var roleNames = await (
                from userRole in _master.UserRoles
                join identityRole in _master.Roles on userRole.RoleId equals identityRole.Id
                where userRole.UserId == route.UserId
                select identityRole.Name)
            .ToListAsync(ct);
        var roleName = roleNames.FirstOrDefault(r => !string.Equals(r, PlatformRoles.PlatformAdmin, StringComparison.Ordinal))
            ?? UserRole.Accountant.ToString();
        var role = Enum.TryParse<UserRole>(roleName, out var parsedRole) ? parsedRole : UserRole.Accountant;

        var access = await _effectivePermissions.GetUserAccessSnapshotAsync(route.UserId, ct);
        if (!access.EffectivePermissions.Contains(Permissions.AI.Chat))
        {
            await TrySendAsync(message.ExternalChatId, NoAiPermissionReply, ct);
            return;
        }

        var connectionString = await _tenantService.GetConnectionStringAsync(route.TenantId, ct);
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogError("Chaîne de connexion introuvable pour le tenant {TenantId} (canal).", route.TenantId);
            await TrySendAsync(message.ExternalChatId, ErrorReply, ct);
            return;
        }

        // Contexte tenant ambiant (chemin AsyncLocal — pas de HttpContext en job Hangfire) puis
        // garde de migrations, dans le même ordre que TenantMiddleware.
        _tenantContext.SetTenant(route.TenantId, connectionString);
        ChannelUserContext.Set(new ChannelUserSnapshot(
            route.UserId, route.TenantId, user.Email, role, access.EffectivePermissions));
        try
        {
            var migrationResult = await _migrationGuard.EnsureMigrationsAppliedAsync(route.TenantId, ct);
            if (migrationResult.IsFailure)
            {
                _logger.LogWarning("Garde de migrations en échec pour le tenant {TenantId} (canal) : {Error}",
                    route.TenantId, migrationResult.Error.Description);
                await TrySendAsync(message.ExternalChatId, ErrorReply, ct);
                return;
            }

            // Accusé immédiat : l'inférence CPU peut durer des dizaines de secondes.
            await TrySendAsync(message.ExternalChatId, ProcessingAckReply, ct);

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var conversationId = await FindOrCreateConversationAsync(route.UserId, nowUtc, ct);

            var chatCommand = new SendChatMessageCommand(
                conversationId,
                text,
                Options: new ChatRequestOptionsDto { ForceReadOnlyTools = true });

            string reply;
            try
            {
                reply = await RunPipelineAsync(chatCommand, route.UserId, traceId, ct);
            }
            catch (OperationCanceledException)
            {
                // Timeout d'orchestration (CancelAfter) ou arrêt du serveur : réponse d'excuse
                // best-effort plutôt qu'un silence.
                reply = TimeoutReply;
            }

            foreach (var chunk in WhatsAppTextFormatter.Split(reply, Math.Max(200, _settings.MaxOutboundMessageChars)))
                await TrySendAsync(message.ExternalChatId, chunk, CancellationToken.None);

            await _linkService.TouchLastSeenAsync(
                route.TenantId, ChannelType.WhatsApp, message.ExternalUserId, CancellationToken.None);
        }
        finally
        {
            ChannelUserContext.Clear();
            _tenantContext.Clear();
        }
    }

    /// <summary>
    /// Consomme le flux du pipeline (agrégation <see cref="ChannelChatStreamAggregator"/>) et
    /// produit le texte WhatsApp final. Ceinture-bretelles : assainissement des noms d'outils
    /// internes et retrait CJK avant formatage.
    /// </summary>
    private async Task<string> RunPipelineAsync(
        SendChatMessageCommand chatCommand, Guid userId, string traceId, CancellationToken ct)
    {
        var aggregate = await ChannelChatStreamAggregator.AggregateAsync(
            _chatHandler.HandleAsync(chatCommand, userId, traceId, ct), ct);

        if (aggregate.Error is not null && aggregate.Content.Length == 0)
        {
            _logger.LogWarning("Pipeline IA en erreur pour un message canal : {Error}", aggregate.Error);
            return ErrorReply;
        }

        var sanitized = AssistantVisibleContentFormatter.SanitizeVisibleProse(aggregate.Content).Trim();
        if (sanitized.Length == 0)
            return EmptyReply;

        return WhatsAppTextFormatter.Format(sanitized);
    }

    private async Task<Guid> FindOrCreateConversationAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        var conversations = await _conversations.GetByUserIdAsync(userId, agentScope: 0, ct);
        var recent = conversations
            .Where(c => c.Title.StartsWith(ConversationTitlePrefix, StringComparison.Ordinal)
                && c.LastMessageAt >= nowUtc - ConversationReuseWindow)
            .OrderByDescending(c => c.LastMessageAt)
            .FirstOrDefault();
        if (recent is not null)
            return recent.Id;

        var conversation = Conversation.Create(
            userId, $"{ConversationTitlePrefix} · {nowUtc:dd/MM HH:mm}", model: null, agentScope: 0);
        await _conversations.AddAsync(conversation, ct);
        return conversation.Id;
    }

    private async Task TrySendAsync(string chatId, string text, CancellationToken ct)
    {
        var sent = await _outbound.SendWhatsAppTextAsync(chatId, text, ct);
        if (!sent)
            _logger.LogWarning("Réponse canal non délivrée ({Length} caractères).", text.Length);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
