using System.Security.Claims;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<NotificationListDto>>> GetList(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var list = await _notificationService.GetListAsync(tenantId.Value, GetRole(), unreadOnly, page, pageSize, cancellationToken);
        return Ok(ApiResponse<NotificationListDto>.Ok(list));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<int>>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var count = await _notificationService.GetUnreadCountAsync(tenantId.Value, GetRole(), cancellationToken);
        return Ok(ApiResponse<int>.Ok(count));
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<ActionResult<ApiResponse<object>>> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var result = await _notificationService.MarkReadAsync(tenantId.Value, GetRole(), notificationId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(null!));
    }

    [HttpPost("read-all")]
    public async Task<ActionResult<ApiResponse<object>>> MarkAllRead(CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        await _notificationService.MarkAllReadAsync(tenantId.Value, GetRole(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(null!));
    }

    private Guid? GetHomeTenantId()
    {
        var claim = User.FindFirstValue("tenant_id");
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private string? GetRole() => User.FindFirstValue(ClaimTypes.Role);
}
