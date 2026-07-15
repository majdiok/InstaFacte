using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Lot C2 — Endpoints backoffice pour le log des emails envoyés et le retry.
///
/// Permissions :
/// <list type="bullet">
///   <item>Lecture : <see cref="PlatformPermissions.AuditRead"/> (les rôles audit voient le log).</item>
///   <item>Retry / SendTest : <see cref="PlatformPermissions.AdminsManage"/> (action admin).</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/platform/email-messages")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformEmailMessagesController : ControllerBase
{
    private readonly IEmailMessageQueryService _query;
    private readonly IEmailService _emailService;
    private readonly ILogger<PlatformEmailMessagesController> _logger;

    public PlatformEmailMessagesController(
        IEmailMessageQueryService query,
        IEmailService emailService,
        ILogger<PlatformEmailMessagesController> logger)
    {
        _query = query;
        _emailService = emailService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "perm:" + PlatformPermissions.AuditRead)]
    [ProducesResponseType(typeof(ApiResponse<EmailMessagesPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] int? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var dto = await _query.ListAsync(tenantId, status, search, page, pageSize, cancellationToken);
        return Ok(ApiResponse<EmailMessagesPageDto>.Ok(dto));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.AuditRead)]
    [ProducesResponseType(typeof(ApiResponse<EmailMessageDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var dto = await _query.GetByIdAsync(id, cancellationToken);
        if (dto is null) return NotFound(ApiResponse<EmailMessageDetailDto>.Fail("Message introuvable."));
        return Ok(ApiResponse<EmailMessageDetailDto>.Ok(dto));
    }

    [HttpPost("{id:guid}/retry")]
    [Authorize(Policy = "perm:" + PlatformPermissions.AdminsManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        var result = await _query.RequeueAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
                ? NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
                : BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));
        }

        _logger.LogInformation("Platform admin retried email message {Id}", id);
        return Ok(ApiResponse<object>.Ok(null!, "Message remis en file."));
    }

    /// <summary>Lance un envoi de test pour vérifier la configuration SMTP.</summary>
    [HttpPost("send-test")]
    [Authorize(Policy = "perm:" + PlatformPermissions.AdminsManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendTest(
        [FromBody] SendTestEmailRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.ToEmail))
            return BadRequest(ApiResponse<object>.Fail("Adresse email requise."));

        var id = await _emailService.EnqueueTemplatedAsync(
            request.ToEmail,
            toName: null,
            templateCode: "test-email",
            model: new Dictionary<string, object?>(),
            relatedTenantId: null,
            cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { messageId = id }, "Email de test mis en file."));
    }
}
