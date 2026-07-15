using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Enums;
using FactuTrust.Application.Configuration;
using FactuTrust.Infrastructure.Services.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Platform-wide AI model configuration. A single model is shared by every tenant
/// (Ollama runs on the shared platform server).
/// </summary>
[ApiController]
[Route("api/platform/ai-settings")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformAiSettingsController : ControllerBase
{
    private readonly IPlatformAiSettingsService _settings;
    private readonly IOllamaClient _ollamaClient;
    private readonly IAiModelRecommender _modelRecommender;
    private readonly OllamaSettings _ollamaSettings;
    private readonly ILogger<PlatformAiSettingsController> _logger;

    public PlatformAiSettingsController(
        IPlatformAiSettingsService settings,
        IOllamaClient ollamaClient,
        IAiModelRecommender modelRecommender,
        IOptions<OllamaSettings> ollamaSettings,
        ILogger<PlatformAiSettingsController> logger)
    {
        _settings = settings;
        _ollamaClient = ollamaClient;
        _modelRecommender = modelRecommender;
        _ollamaSettings = ollamaSettings.Value;
        _logger = logger;
    }

    /// <summary>Configured platform model + installed Ollama models + hardware recommendation.</summary>
    [HttpGet]
    [Authorize(Policy = "perm:" + PlatformPermissions.AiManage)]
    [ProducesResponseType(typeof(ApiResponse<PlatformAiSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var configured = await _settings.GetDefaultModelRefAsync(cancellationToken);
        var importModel = await _settings.GetInvoiceImportModelRefAsync(cancellationToken);
        var inferenceDevice = await _settings.GetInferenceDeviceAsync(cancellationToken);

        var models = new List<UnifiedAiModelInfo>();
        try
        {
            var ollamaModels = await _ollamaClient.ListModelsAsync(cancellationToken);
            foreach (var m in ollamaModels)
            {
                models.Add(new UnifiedAiModelInfo(
                    $"{ModelRef.OllamaPrefix}{m.Name}",
                    "ollama",
                    m.Name,
                    m.Size,
                    m.ModifiedAt,
                    SupportsVision: AiModelCapabilityDetector.DetectVisionSupport(m.Name)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list Ollama models for platform AI settings.");
        }

        AiModelRecommendationDto? recommendation = null;
        try
        {
            recommendation = await _modelRecommender.RecommendBestLocalModelAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to compute the model recommendation for platform AI settings.");
        }

        var visionServer = string.IsNullOrWhiteSpace(_ollamaSettings.InvoiceImportVisionModel)
            ? null
            : _ollamaSettings.InvoiceImportVisionModel.Trim();
        var effectiveAssistantModel = string.IsNullOrWhiteSpace(configured)
            ? _ollamaSettings.DefaultModel
            : configured;
        var isOllamaAssistant = ModelRef.Parse(effectiveAssistantModel).Kind == LlmProviderKind.Ollama;

        var dto = new PlatformAiSettingsDto(
            configured,
            importModel,
            visionServer,
            inferenceDevice,
            isOllamaAssistant,
            models,
            recommendation);
        return Ok(ApiResponse<PlatformAiSettingsDto>.Ok(dto));
    }

    /// <summary>Set the platform default model. An empty value clears it (server default applies).</summary>
    [HttpPut]
    [Authorize(Policy = "perm:" + PlatformPermissions.AiManage)]
    [ProducesResponseType(typeof(ApiResponse<PlatformAiSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        [FromBody] UpdatePlatformAiSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("Non authentifié."));

        if (!string.IsNullOrWhiteSpace(request.ModelRef))
        {
            var parsed = ModelRef.Parse(request.ModelRef);
            if (string.IsNullOrEmpty(parsed.CanonicalModelRef))
                return BadRequest(ApiResponse<object>.Fail("Référence de modèle invalide."));
        }

        if (request.ModelRef is not null)
        {
            await _settings.SetDefaultModelRefAsync(request.ModelRef, actorId, cancellationToken);
            _logger.LogInformation("Platform admin {ActorId} updated the default AI model", actorId);
        }

        if (request.InvoiceImportModelRef is not null)
        {
            if (!string.IsNullOrWhiteSpace(request.InvoiceImportModelRef))
            {
                var parsedImport = ModelRef.Parse(request.InvoiceImportModelRef);
                if (string.IsNullOrEmpty(parsedImport.CanonicalModelRef))
                    return BadRequest(ApiResponse<object>.Fail("Référence de modèle d'import invalide."));
            }

            await _settings.SetInvoiceImportModelRefAsync(request.InvoiceImportModelRef, actorId, cancellationToken);
            _logger.LogInformation("Platform admin {ActorId} updated the invoice import AI model", actorId);
        }

        if (request.InferenceDevice is { } device)
        {
            if (!Enum.IsDefined(device))
                return BadRequest(ApiResponse<object>.Fail("Moteur d'inférence invalide."));

            await _settings.SetInferenceDeviceAsync(device, actorId, cancellationToken);
            _modelRecommender.InvalidateCache();
            _logger.LogInformation(
                "Platform admin {ActorId} updated the Ollama inference device to {Device}",
                actorId,
                device);
        }

        return await Get(cancellationToken);
    }
}
