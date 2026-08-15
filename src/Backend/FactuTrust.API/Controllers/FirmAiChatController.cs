using System.Security.Claims;
using System.Text.Json;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.Commands;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Queries;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.AI;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Surface HTTP de l'agent « Chef de mission » (cabinet en mode NATIF).
///
/// <para>
/// Contrôleur distinct de <see cref="AiChatController"/> à dessein. Ouvrir l'assistant tenant aux
/// cabinets aurait imposé de leur accorder <c>ai:chat</c>, donc les 83 outils du catalogue dossier
/// sur la base quasi vide du cabinet — et aurait invalidé la garde
/// <c>DelegatedPermissionCatalogAiTests.FirmNativePermissions_excludes_ai_chat</c>. Ici la surface
/// est étroite, séparément permissionnée, et le scope est imposé côté serveur.
/// </para>
///
/// <para>
/// La route n'est volontairement PAS déclarée dans <c>AccountingFirmMasterOnlyRoutes</c> : ce
/// contournement laisserait <c>ITenantContext</c> vide, et la persistance de conversation (qui vit
/// dans la base propre du cabinet, provisionnée à l'inscription) échouerait.
/// </para>
///
/// <para>
/// La boucle SSE ci-dessous duplique celle d'<see cref="AiChatController"/>. C'est délibéré et
/// assumé : factoriser aurait exigé de modifier le seul chemin IA en production, qu'aucun test
/// n'encadre. À consolider une fois ce chemin stabilisé.
/// </para>
/// </summary>
[Route("api/firm/ai")]
[ApiController]
[Authorize(Roles = $"{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
[Authorize(Policy = PermissionPolicies.FirmAiChat)]
[Filters.RequireAccountingFirmsFeature]
public class FirmAiChatController : ControllerBase
{
    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IMediator _mediator;
    private readonly SendChatMessageHandler _chatHandler;
    private readonly IPlatformAiSettingsService _platformAiSettings;
    private readonly AccountingFirmsOptions _accountingFirmsOptions;
    private readonly OllamaSettings _ollamaSettings;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<FirmAiChatController> _logger;

    public FirmAiChatController(
        IMediator mediator,
        SendChatMessageHandler chatHandler,
        IPlatformAiSettingsService platformAiSettings,
        IOptions<AccountingFirmsOptions> accountingFirmsOptions,
        IOptions<OllamaSettings> ollamaSettings,
        IHostEnvironment environment,
        ILogger<FirmAiChatController> logger)
    {
        _mediator = mediator;
        _chatHandler = chatHandler;
        _platformAiSettings = platformAiSettings;
        _accountingFirmsOptions = accountingFirmsOptions.Value;
        _ollamaSettings = ollamaSettings.Value;
        _environment = environment;
        _logger = logger;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private bool IsAgentEnabled => _accountingFirmsOptions.Enabled && _accountingFirmsOptions.FirmAgentEnabled;

    /// <summary>Chat avec le Chef de mission (SSE). Le scope est imposé serveur, jamais lu du client.</summary>
    [HttpPost("chat")]
    [EnableRateLimiting("ai")]
    public async Task Chat([FromBody] AiChatHttpRequestDto request, CancellationToken cancellationToken)
    {
        if (!IsAgentEnabled)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers.Append("X-Trace-Id", HttpContext.TraceIdentifier);

        // Le scope est FORCÉ ici : un client ne peut pas demander un autre expert par cette route,
        // ni demander le Chef de mission par la route tenant (le scope y est refusé en délégué).
        var options = (request.Options ?? new ChatRequestOptionsDto()) with
        {
            AssistantMode = AssistantMode.Default,
            AgentScope = AssistantAgentScope.FirmMission
        };

        var command = new SendChatMessageCommand(
            request.ConversationId,
            request.Message,
            request.Model,
            request.UiContext,
            options,
            request.Attachments);

        var userId = GetUserId();
        var correlationId = HttpContext.TraceIdentifier;

        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var writeLock = new SemaphoreSlim(1, 1);
        var heartbeatSeconds = Math.Clamp(_ollamaSettings.SseHeartbeatIntervalSeconds, 0, 120);

        Task? heartbeatTask = null;
        if (heartbeatSeconds > 0)
        {
            heartbeatTask = Task.Run(
                () => HeartbeatLoopAsync(heartbeatSeconds, writeLock, heartbeatCts.Token),
                CancellationToken.None);
        }

        try
        {
            await foreach (var chunk in _chatHandler.HandleAsync(command, userId, correlationId, cancellationToken))
            {
                await writeLock.WaitAsync(cancellationToken);
                try
                {
                    var json = JsonSerializer.Serialize(chunk, SseJsonOptions);
                    await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
                finally
                {
                    writeLock.Release();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Déconnexion réelle du client : rien à émettre.
        }
        catch (Exception ex)
        {
            LogChatException(ex);
            var errorEvent = ChatStreamEvent.ErrorEvent(ResolveChatErrorMessageForClient(ex));
            var errorJson = JsonSerializer.Serialize(errorEvent, SseJsonOptions);
            try
            {
                await writeLock.WaitAsync(cancellationToken);
                try
                {
                    await Response.WriteAsync($"data: {errorJson}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
                finally
                {
                    writeLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // Réponse plus inscriptible.
            }
        }
        finally
        {
            await heartbeatCts.CancelAsync();
            if (heartbeatTask is not null)
            {
                try
                {
                    await heartbeatTask;
                }
                catch (OperationCanceledException)
                {
                    // ignoré
                }
            }
        }
    }

    /// <summary>Conversations du Chef de mission uniquement : le scope n'est pas paramétrable.</summary>
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ConversationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConversations(CancellationToken cancellationToken)
    {
        if (!IsAgentEnabled)
            return NotFound();

        var result = await _mediator.Send(
            new GetConversationsQuery(GetUserId(), (int)AssistantAgentScope.FirmMission),
            cancellationToken);

        return result.IsSuccess
            ? Ok(ApiResponse<IReadOnlyList<ConversationDto>>.Ok(result.Value))
            : BadRequest(ApiResponse<object>.Fail(result.Error.Description));
    }

    [HttpGet("conversations/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ConversationDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConversation(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAgentEnabled)
            return NotFound();

        var result = await _mediator.Send(new GetConversationQuery(id), cancellationToken);

        return result.IsFailure
            ? NotFound(ApiResponse<object>.Fail(result.Error.Description))
            : Ok(ApiResponse<ConversationDetailDto>.Ok(result.Value));
    }

    /// <summary>
    /// Supprime une conversation du Chef de mission. Le panneau de discussion propose la
    /// suppression quelle que soit la surface : sans cet endpoint, l'action échouerait en 405.
    /// </summary>
    [HttpDelete("conversations/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConversation(Guid id, CancellationToken cancellationToken)
    {
        if (!IsAgentEnabled)
            return NotFound();

        var result = await _mediator.Send(new DeleteConversationCommand(id), cancellationToken);

        return result.IsFailure
            ? NotFound(ApiResponse<object>.Fail(result.Error.Description))
            : NoContent();
    }

    /// <summary>
    /// Modèle IA actif de la plateforme. Répliqué ici parce que l'équivalent tenant exige
    /// <c>ai:chat</c>, absent du JWT cabinet natif : sans cet endpoint, le panneau afficherait un
    /// 403 et perdrait le libellé du modèle. Lecture seule, aucune donnée de dossier.
    /// </summary>
    [HttpGet("active-model")]
    [ProducesResponseType(typeof(ApiResponse<AiActiveModelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveModel(CancellationToken cancellationToken)
    {
        if (!IsAgentEnabled)
            return NotFound();

        var configured = await _platformAiSettings.GetDefaultModelRefAsync(cancellationToken);

        var fallback = string.IsNullOrWhiteSpace(_ollamaSettings.DefaultModel)
            ? "mistral"
            : _ollamaSettings.DefaultModel.Trim();
        var rawModel = string.IsNullOrWhiteSpace(configured)
            ? $"{ModelRef.OllamaPrefix}{fallback}"
            : configured;

        var parsed = ModelRef.Parse(rawModel);
        var supportsVision = AiModelCapabilityDetector.DetectVisionSupport(parsed);

        return Ok(ApiResponse<AiActiveModelDto>.Ok(
            new AiActiveModelDto(parsed.CanonicalModelRef, parsed.ProviderModelId, supportsVision)));
    }

    private async Task HeartbeatLoopAsync(int intervalSeconds, SemaphoreSlim writeLock, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
                await writeLock.WaitAsync(cancellationToken);
                try
                {
                    var json = JsonSerializer.Serialize(ChatStreamEvent.Heartbeat(), SseJsonOptions);
                    await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
                finally
                {
                    writeLock.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Arrêt normal en fin de flux ou à la déconnexion du client.
        }
    }

    private void LogChatException(Exception ex)
    {
        var traceId = HttpContext.TraceIdentifier;

        if (ex is DbUpdateException dbEx)
        {
            _logger.LogError(dbEx,
                "Échec de persistance assistant cabinet (DbUpdateException). TraceId={TraceId} Inner={Inner}",
                traceId, dbEx.InnerException?.Message);
            return;
        }

        if (ex is OllamaRequestException ore)
        {
            _logger.LogWarning("Erreur appel Ollama (assistant cabinet). TraceId={TraceId} Status={Status} Detail={Detail}",
                traceId, ore.HttpStatusCode, ore.Message);
            return;
        }

        if (ex is OpenAiCompatibleRequestException oa)
        {
            _logger.LogWarning("Erreur appel fournisseur OpenAI-compatible (assistant cabinet). TraceId={TraceId} Status={Status}",
                traceId, oa.HttpStatusCode);
            return;
        }

        _logger.LogError(ex, "Erreur flux assistant cabinet. TraceId={TraceId}", traceId);
    }

    private string ResolveChatErrorMessageForClient(Exception ex)
    {
        if (ex is OllamaRequestException ore)
            return ore.UserMessage;

        if (ex is OpenAiCompatibleRequestException oa)
            return oa.UserMessage;

        if (ex is UnauthorizedAccessException ua)
            return ua.Message;

        if (ex is OperationCanceledException or TimeoutException)
            return "Le service IA est momentanément saturé ou a mis trop de temps à répondre. Réessayez dans un instant.";

        if (ex is DbUpdateException dxe)
        {
            return _environment.IsDevelopment()
                ? $"{dxe.Message} ({dxe.InnerException?.Message})"
                : "Impossible d'enregistrer la conversation. Réessayez dans un instant. Si le problème persiste, contactez l'administrateur.";
        }

        return _environment.IsDevelopment()
            ? ex.Message
            : "Une erreur inattendue s'est produite avec l'assistant. Réessayez dans un instant.";
    }
}
