using System.Text.Json;
using System.Threading.Channels;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.Studio.Ai;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers.Studio;

/// <summary>
/// Plans Studio IA (flux plan → aperçu → confirmation). La confirmation est un endpoint REST
/// DÉTERMINISTE : aucun LLM dans la boucle — le plan approuvé est exécuté tel quel et la
/// progression (<c>StudioBuildStep</c>) est streamée en SSE au fur et à mesure.
/// </summary>
[ApiController]
[Route("api/studio/ai/plans")]
[Authorize]
[Authorize(Policy = PermissionPolicies.StudioDesignEntities)]
public sealed class StudioAiPlansController : ControllerBase
{
    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMediator _mediator;
    private readonly OllamaSettings _ollamaSettings;

    public StudioAiPlansController(IMediator mediator, IOptions<OllamaSettings> ollamaSettings)
    {
        _mediator = mediator;
        _ollamaSettings = ollamaSettings.Value;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetStudioAiPlanQuery(id), cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<StudioAiPlanDto>.Ok(result.Value));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CancelStudioAiPlanCommand(id), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<StudioAiPlanDto>.Ok(result.Value, "Plan annulé."));
    }

    /// <summary>
    /// Exécute le plan confirmé et streame la progression en SSE : événements
    /// <c>studio_progress</c> (étapes live), puis <c>studio_result</c> (payload final) ou
    /// <c>error</c>, et enfin <c>done</c>.
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    public async Task Confirm(Guid id, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        if (!_ollamaSettings.EnableStudioAiPlanPreview)
        {
            await WriteEventAsync(ChatStreamEvent.ErrorEvent(
                "Le flux d'aperçu Studio n'est pas activé."), cancellationToken);
            return;
        }

        // Les étapes émises par l'orchestrateur (thread du handler) transitent par un channel
        // que cette action draine vers la réponse SSE — progression réellement temps réel.
        var channel = Channel.CreateUnbounded<StudioBuildStep>();
        var progress = new ChannelBuildProgress(channel.Writer);

        // CancellationToken.None : une exécution confirmée va jusqu'au bout même si l'onglet se ferme
        // (sinon le plan resterait bloqué en Executing) ; seul le flux SSE suit la déconnexion.
        var sendTask = _mediator.Send(new ConfirmStudioAiPlanCommand(id, progress), CancellationToken.None);
        _ = sendTask.ContinueWith(
            _ => channel.Writer.TryComplete(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        try
        {
            await foreach (var step in channel.Reader.ReadAllAsync(cancellationToken))
            {
                await WriteEventAsync(
                    ChatStreamEvent.StudioProgressEvent(JsonSerializer.Serialize(step, SseJsonOptions)),
                    cancellationToken);
            }

            var result = await sendTask;
            if (result.IsSuccess)
            {
                await WriteEventAsync(new ChatStreamEvent
                {
                    Type = "studio_result",
                    Content = JsonSerializer.Serialize(result.Value, SseJsonOptions)
                }, cancellationToken);
            }
            else
            {
                await WriteEventAsync(ChatStreamEvent.ErrorEvent(result.Error.Description), cancellationToken);
            }

            await WriteEventAsync(new ChatStreamEvent { Type = "done" }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Déconnexion du client : on attend la fin de l'exécution AVANT de libérer la requête
            // (le scope DI du handler doit survivre) ; le statut final reste consultable via GET.
            try { await sendTask; }
            catch { /* le résultat est persistant sur le plan */ }
        }
    }

    private async Task WriteEventAsync(ChatStreamEvent evt, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(evt, SseJsonOptions);
        await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private sealed class ChannelBuildProgress : IStudioBuildProgress
    {
        private readonly ChannelWriter<StudioBuildStep> _writer;

        public ChannelBuildProgress(ChannelWriter<StudioBuildStep> writer) => _writer = writer;

        public void Report(StudioBuildStep step) => _writer.TryWrite(step);
    }
}
