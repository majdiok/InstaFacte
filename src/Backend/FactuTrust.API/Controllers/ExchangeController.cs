using System.Security.Claims;
using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/exchanges")]
[Authorize]
[Filters.RequireAccountingFirmsFeature]
public sealed class ExchangeController : ControllerBase
{
    private readonly IExchangeService _exchange;

    public ExchangeController(IExchangeService exchange)
    {
        _exchange = exchange;
    }

    [HttpGet]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ExchangeThreadListItemDto>>>> List(CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var items = await _exchange.ListThreadsAsync(ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.FirmScope, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ExchangeThreadListItemDto>>.Ok(items));
    }

    [HttpGet("unread-summary")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<ExchangeUnreadSummaryDto>>> UnreadSummary(CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var summary = await _exchange.GetUnreadSummaryAsync(
            ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId, ctx.Value.Role, ctx.Value.FirmScope, cancellationToken);
        return Ok(ApiResponse<ExchangeUnreadSummaryDto>.Ok(summary));
    }

    [HttpGet("{threadId:guid}")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<ExchangeThreadDetailDto>>> Get(Guid threadId, CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var result = await _exchange.GetThreadAsync(
            threadId, ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId, ctx.Value.Role, ctx.Value.FirmScope, cancellationToken);
        return Map(result);
    }

    [HttpPost("ensure")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<ExchangeThreadDetailDto>>> Ensure(
        [FromBody] EnsureExchangeThreadDto? dto, CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var result = await _exchange.EnsureThreadAsync(
            ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId, ctx.Value.DisplayName, ctx.Value.Role,
            ctx.Value.FirmScope, dto?.FirmClientAssignmentId, cancellationToken);
        return Map(result);
    }

    [HttpPost("{threadId:guid}/close")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)}")]
    public async Task<ActionResult<ApiResponse<object>>> Close(Guid threadId, CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var result = await _exchange.CloseThreadAsync(
            threadId, ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId, ctx.Value.DisplayName,
            ctx.Value.Role, ctx.Value.FirmScope, cancellationToken);
        return MapEmpty(result, "Échange clos");
    }

    [HttpPost("{threadId:guid}/reopen")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)}")]
    public async Task<ActionResult<ApiResponse<object>>> Reopen(Guid threadId, CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var result = await _exchange.ReopenThreadAsync(
            threadId, ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId, ctx.Value.DisplayName,
            ctx.Value.Role, ctx.Value.FirmScope, cancellationToken);
        return MapEmpty(result, "Échange rouvert");
    }

    [HttpGet("{threadId:guid}/messages")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ExchangeMessageDto>>>> Messages(
        Guid threadId, [FromQuery] DateTime? after, CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var result = await _exchange.GetMessagesAsync(
            threadId, ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId, ctx.Value.Role,
            ctx.Value.FirmScope, after, cancellationToken);
        return Map(result);
    }

    [HttpPost("{threadId:guid}/messages")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<ExchangeMessageDto>>> SendMessage(
        Guid threadId, [FromBody] SendExchangeMessageDto dto, CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var result = await _exchange.SendMessageAsync(
            threadId, ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId, ctx.Value.DisplayName,
            ctx.Value.Role, ctx.Value.FirmScope, dto, cancellationToken);
        return Map(result);
    }

    [HttpPost("{threadId:guid}/messages/{messageId:guid}/read")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<object>>> MarkRead(
        Guid threadId, Guid messageId, CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var result = await _exchange.MarkMessageReadAsync(
            threadId, messageId, ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId,
            ctx.Value.Role, ctx.Value.FirmScope, cancellationToken);
        return MapEmpty(result);
    }

    [HttpGet("{threadId:guid}/requests")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ExchangeRequestDto>>>> ListRequests(
        Guid threadId, CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var result = await _exchange.ListRequestsAsync(
            threadId, ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId, ctx.Value.Role,
            ctx.Value.FirmScope, cancellationToken);
        return Map(result);
    }

    [HttpPost("{threadId:guid}/requests")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<ExchangeRequestDto>>> CreateRequest(
        Guid threadId, [FromBody] CreateExchangeRequestDto dto, CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var result = await _exchange.CreateRequestAsync(
            threadId, ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId, ctx.Value.DisplayName,
            ctx.Value.Role, ctx.Value.FirmScope, dto, cancellationToken);
        return Map(result);
    }

    [HttpPost("{threadId:guid}/requests/{requestId:guid}/status")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<ExchangeRequestDto>>> ChangeRequestStatus(
        Guid threadId, Guid requestId, [FromBody] ChangeExchangeRequestStatusDto dto, CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var result = await _exchange.ChangeRequestStatusAsync(
            threadId, requestId, ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId, ctx.Value.DisplayName,
            ctx.Value.Role, ctx.Value.FirmScope, dto, cancellationToken);
        return Map(result);
    }

    [HttpPost("{threadId:guid}/requests/{requestId:guid}/assign")]
    [Authorize(Roles = $"{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<ExchangeRequestDto>>> AssignRequest(
        Guid threadId, Guid requestId, [FromBody] AssignExchangeRequestDto dto, CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var result = await _exchange.AssignRequestAsync(
            threadId, requestId, ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId, ctx.Value.DisplayName,
            ctx.Value.Role, ctx.Value.FirmScope, dto, cancellationToken);
        return Map(result);
    }

    [HttpGet("{threadId:guid}/tasks")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ExchangeTaskDto>>>> ListTasks(
        Guid threadId, CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var result = await _exchange.ListTasksAsync(
            threadId, ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId, ctx.Value.Role,
            ctx.Value.FirmScope, cancellationToken);
        return Map(result);
    }

    [HttpPost("{threadId:guid}/tasks")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<ExchangeTaskDto>>> CreateTask(
        Guid threadId, [FromBody] CreateExchangeTaskDto dto, CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var result = await _exchange.CreateTaskAsync(
            threadId, ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId, ctx.Value.DisplayName,
            ctx.Value.Role, ctx.Value.FirmScope, dto, cancellationToken);
        return Map(result);
    }

    [HttpPost("{threadId:guid}/tasks/{taskId:guid}/status")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<ExchangeTaskDto>>> ChangeTaskStatus(
        Guid threadId, Guid taskId, [FromBody] ChangeExchangeTaskStatusDto dto, CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var result = await _exchange.ChangeTaskStatusAsync(
            threadId, taskId, ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId, ctx.Value.DisplayName,
            ctx.Value.Role, ctx.Value.FirmScope, dto, cancellationToken);
        return Map(result);
    }

    [HttpGet("{threadId:guid}/documents")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ExchangeDocumentDto>>>> ListDocuments(
        Guid threadId, CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var result = await _exchange.ListDocumentsAsync(
            threadId, ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId, ctx.Value.Role,
            ctx.Value.FirmScope, cancellationToken);
        return Map(result);
    }

    [HttpPost("{threadId:guid}/documents")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ExchangeDocumentDto>>> UploadDocument(
        Guid threadId,
        IFormFile file,
        [FromQuery] Guid? messageId,
        [FromQuery] Guid? requestId,
        [FromQuery] Guid? taskId,
        CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<ExchangeDocumentDto>.Fail("Fichier requis"));

        await using var stream = file.OpenReadStream();
        var result = await _exchange.UploadDocumentAsync(
            threadId, ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId, ctx.Value.DisplayName,
            ctx.Value.Role, ctx.Value.FirmScope, file.FileName, file.ContentType, stream,
            messageId, requestId, taskId, cancellationToken);
        return Map(result);
    }

    [HttpGet("{threadId:guid}/documents/{documentId:guid}/download")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<IActionResult> DownloadDocument(
        Guid threadId, Guid documentId, CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var result = await _exchange.DownloadDocumentAsync(
            threadId, documentId, ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId,
            ctx.Value.Role, ctx.Value.FirmScope, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return File(result.Value.Stream, result.Value.ContentType, result.Value.FileName);
    }

    [HttpDelete("{threadId:guid}/documents/{documentId:guid}")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteDocument(
        Guid threadId, Guid documentId, CancellationToken cancellationToken)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var result = await _exchange.DeleteDocumentAsync(
            threadId, documentId, ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId,
            ctx.Value.Role, ctx.Value.FirmScope, cancellationToken);
        return MapEmpty(result, "Document supprimé");
    }

    [HttpGet("{threadId:guid}/history")]
    [Authorize(Roles = $"{nameof(UserRole.Administrator)},{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ExchangeAuditEventDto>>>> History(
        Guid threadId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var ctx = ResolveCaller();
        if (ctx is null) return Unauthorized();

        var result = await _exchange.GetHistoryAsync(
            threadId, ctx.Value.TenantId, ctx.Value.Kind, ctx.Value.UserId, ctx.Value.Role,
            ctx.Value.FirmScope, page, pageSize, cancellationToken);
        return Map(result);
    }

    private CallerContext? ResolveCaller()
    {
        var tenantClaim = User.FindFirstValue("tenant_id");
        var userClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(tenantClaim, out var tenantId) || !Guid.TryParse(userClaim, out var userId))
            return null;

        var role = User.FindFirstValue(ClaimTypes.Role);
        var kindClaim = User.FindFirstValue("tenant_kind");
        var kind = string.Equals(kindClaim, "accountingFirm", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kindClaim, nameof(TenantKind.AccountingFirm), StringComparison.OrdinalIgnoreCase)
            ? TenantKind.AccountingFirm
            : TenantKind.Company;

        // Also detect firm by role when claim missing
        if (role is nameof(UserRole.FirmManager) or nameof(UserRole.FirmAccountant))
            kind = TenantKind.AccountingFirm;

        var first = User.FindFirstValue("first_name") ?? User.FindFirstValue(ClaimTypes.GivenName) ?? "";
        var last = User.FindFirstValue("last_name") ?? User.FindFirstValue(ClaimTypes.Surname) ?? "";
        var display = $"{first} {last}".Trim();
        if (string.IsNullOrEmpty(display))
            display = User.Identity?.Name ?? "Utilisateur";

        FirmDossierAccessScope? firmScope = kind == TenantKind.AccountingFirm && role is not null
            ? FirmDossierAccessScope.ForUser(userId, role)
            : null;

        return new CallerContext(tenantId, userId, kind, role, display, firmScope);
    }

    private ActionResult<ApiResponse<T>> Map<T>(Domain.Common.Result<T> result)
    {
        if (result.IsFailure)
        {
            if (result.Error.Code.StartsWith("Forbidden", StringComparison.Ordinal))
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<T>.Fail(result.Error.Description));
            if (result.Error.Code.Contains("NotFound", StringComparison.Ordinal))
                return NotFound(ApiResponse<T>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<T>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<T>.Ok(result.Value));
    }

    private ActionResult<ApiResponse<object>> MapEmpty(Domain.Common.Result result, string? message = null)
    {
        if (result.IsFailure)
        {
            if (result.Error.Code.StartsWith("Forbidden", StringComparison.Ordinal))
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<object>.Ok(null!, message));
    }

    private readonly record struct CallerContext(
        Guid TenantId,
        Guid UserId,
        TenantKind Kind,
        string? Role,
        string DisplayName,
        FirmDossierAccessScope? FirmScope);
}
