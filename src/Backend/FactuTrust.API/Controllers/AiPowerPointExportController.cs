using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Commands;
using FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Queries;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FactuTrust.API.Controllers;

/// <summary>
/// HTTP endpoints for generating, downloading and listing PowerPoint exports built from
/// AI assistant responses. The controller is intentionally thin: every domain operation is
/// delegated to MediatR command/query handlers and storage services.
/// </summary>
[Route("api/ai/exports/powerpoint")]
[ApiController]
[Authorize(Policy = PermissionPolicies.AiChat)]
[EnableRateLimiting("ai")]
public sealed class AiPowerPointExportController : ControllerBase
{
    private const string PptxContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
    private const long InlineMaxBytes = 6L * 1024L * 1024L; // stream inline up to 6 MB to keep round-trip latency low

    private readonly IMediator _mediator;
    private readonly IExportStorageService _storage;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<AiPowerPointExportController> _logger;

    public AiPowerPointExportController(
        IMediator mediator,
        IExportStorageService storage,
        ITenantContext tenantContext,
        ILogger<AiPowerPointExportController> logger)
    {
        _mediator = mediator;
        _storage = storage;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Generates a new PowerPoint deck from the selected assistant responses. Small decks are
    /// returned inline (status 200); larger decks return a JSON envelope with a signed
    /// time-limited download link.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PowerPointExportResponseDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Generate(
        [FromBody] PowerPointExportRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(ApiResponse<object>.Fail("Le corps de la requête est invalide."));

        var userId = TryGetUserId();
        if (userId is null) return Unauthorized();

        var tenantId = _tenantContext.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return BadRequest(ApiResponse<object>.Fail("Tenant non résolu dans la requête."));

        var downloadUrlTemplate = BuildDownloadUrlTemplate();
        var command = new GeneratePowerPointCommand(request, userId.Value, tenantId.Value, downloadUrlTemplate);

        Result_GenerationOutcome outcome;
        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            outcome = ToOutcome(result);
        }
        catch (FluentValidation.ValidationException vex)
        {
            IEnumerable<string> messages = vex.Errors.Select(e => e.ErrorMessage);
            return BadRequest(FactuTrust.Application.DTOs.ApiResponse<object>.Fail(messages, "Validation"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected PowerPoint export failure (user {UserId})", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail("Erreur inattendue lors de la génération."));
        }

        if (!outcome.Success)
            return outcome.ToActionResult(this);

        var dto = outcome.Result!;

        // Inline-stream small decks; force download link for everything else.
        if (dto.SizeBytes <= InlineMaxBytes)
        {
            Response.Headers.Append("X-Export-Id", dto.ExportId.ToString("D"));
            Response.Headers.Append("X-Export-Slides", dto.SlideCount.ToString());
            Response.Headers.Append("X-Export-Expires", dto.ExpiresAt.ToString("O"));
            Response.Headers.Append("X-Export-Download-Url", dto.DownloadUrl);
            return File(dto.Content, PptxContentType, dto.FileName);
        }

        var envelope = new PowerPointExportResponseDto
        {
            ExportId = dto.ExportId,
            FileName = dto.FileName,
            SizeBytes = dto.SizeBytes,
            SlideCount = dto.SlideCount,
            GeneratedAt = dto.GeneratedAt,
            ExpiresAt = dto.ExpiresAt,
            DownloadUrl = dto.DownloadUrl
        };
        return StatusCode(StatusCodes.Status202Accepted, ApiResponse<PowerPointExportResponseDto>.Ok(envelope));
    }

    /// <summary>
    /// Downloads a previously generated PowerPoint deck using a signed token. The token is
    /// scoped to the tenant + export id and expires after a short TTL (Data Protection signed).
    /// </summary>
    [HttpGet("{exportId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        [FromRoute] Guid exportId,
        [FromQuery(Name = "token")] string? token,
        CancellationToken cancellationToken)
    {
        if (exportId == Guid.Empty)
            return BadRequest(ApiResponse<object>.Fail("Identifiant d'export invalide."));
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(ApiResponse<object>.Fail("Jeton de téléchargement manquant."));

        var stored = await _storage.ResolveAsync(exportId, token, cancellationToken);
        if (stored is null)
            return NotFound(ApiResponse<object>.Fail("Lien de téléchargement invalide ou expiré.", "Export.NotFound"));

        return File(stored.Content, stored.ContentType, stored.FileName);
    }

    /// <summary>
    /// Returns the catalogue of available PowerPoint templates so the UI can render preview cards.
    /// </summary>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PowerPointTemplateInfoDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTemplates(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListPowerPointTemplatesQuery(), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<IReadOnlyList<PowerPointTemplateInfoDto>>.Ok(result.Value));
    }

    /// <summary>
    /// Returns a structural preview of an assistant response (KPI/table/section counts and slide outline).
    /// </summary>
    [HttpGet("preview")]
    [ProducesResponseType(typeof(ApiResponse<PowerPointResponsePreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Preview(
        [FromQuery] Guid conversationId,
        [FromQuery] Guid messageId,
        [FromQuery] string? customTitle,
        CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();
        if (userId is null) return Unauthorized();

        var result = await _mediator.Send(
            new PreviewPowerPointExportQuery(conversationId, messageId, userId.Value, customTitle),
            cancellationToken);

        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<PowerPointResponsePreviewDto>.Ok(result.Value));
    }

    /// <summary>
    /// Paginated history of PowerPoint exports for the current user. Used by the "Historique" tab
    /// in the assistant dialog.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PowerPointExportAuditDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> History(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = TryGetUserId();
        if (userId is null) return Unauthorized();

        var result = await _mediator.Send(new GetPowerPointExportHistoryQuery(userId.Value, page, pageSize), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));

        return Ok(ApiResponse<PagedResult<PowerPointExportAuditDto>>.Ok(result.Value));
    }

    private Guid? TryGetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private string BuildDownloadUrlTemplate()
    {
        // Build a host-relative URL so the frontend can append the API base if needed. We do not
        // expose absolute URLs from the backend to avoid leaking internal hostnames in audit logs.
        return "/api/ai/exports/powerpoint/{exportId}?token={token}";
    }

    private static Result_GenerationOutcome ToOutcome(FactuTrust.Domain.Common.Result<GeneratePowerPointCommandResult> result)
    {
        if (result.IsSuccess)
            return new Result_GenerationOutcome(true, result.Value, null, null);
        return new Result_GenerationOutcome(false, null, result.Error.Code, result.Error.Description);
    }

    private sealed record Result_GenerationOutcome(bool Success, GeneratePowerPointCommandResult? Result, string? ErrorCode, string? ErrorMessage)
    {
        public IActionResult ToActionResult(ControllerBase controller)
        {
            if (ErrorCode is null)
                return controller.BadRequest(ApiResponse<object>.Fail("Erreur inconnue."));

            return ErrorCode switch
            {
                "Forbidden" => controller.StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail(ErrorMessage ?? "Accès refusé.", ErrorCode)),
                "PowerPoint.ConversationNotFound" or "PowerPoint.MessageNotFound" =>
                    controller.NotFound(ApiResponse<object>.Fail(ErrorMessage ?? "Ressource introuvable.", ErrorCode)),
                _ => controller.BadRequest(ApiResponse<object>.Fail(ErrorMessage ?? "Erreur métier.", ErrorCode))
            };
        }
    }
}
