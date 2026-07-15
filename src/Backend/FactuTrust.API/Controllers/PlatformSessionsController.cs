using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Lot B4 — Gestion des sessions actives plateforme.
///
/// Endpoints :
/// <list type="bullet">
///   <item><c>GET /api/platform/sessions</c> — liste paginée des sessions plateforme.</item>
///   <item><c>GET /api/platform/sessions/users/{userId}</c> — sessions d'un user précis.</item>
///   <item><c>POST /api/platform/sessions/{id}/revoke</c> — révoque une session.</item>
///   <item><c>POST /api/platform/sessions/users/{userId}/revoke-all</c> — révoque toutes les sessions du user.</item>
/// </list>
///
/// Permissions :
/// <list type="bullet">
///   <item>Lecture : <see cref="PlatformPermissions.SecurityRead"/>.</item>
///   <item>Révocation : <see cref="PlatformPermissions.AdminsManage"/> (action admin sensible).</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/platform/sessions")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformSessionsController : ControllerBase
{
    private readonly IUserSessionService _sessions;
    private readonly ILogger<PlatformSessionsController> _logger;

    public PlatformSessionsController(IUserSessionService sessions, ILogger<PlatformSessionsController> logger)
    {
        _sessions = sessions;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "perm:" + PlatformPermissions.SecurityRead)]
    [ProducesResponseType(typeof(ApiResponse<UserSessionsPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool? activeOnly = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _sessions.ListPlatformSessionsAsync(activeOnly, page, pageSize, cancellationToken);
        return Ok(ApiResponse<UserSessionsPageDto>.Ok(result));
    }

    [HttpGet("users/{userId:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SecurityRead)]
    [ProducesResponseType(typeof(ApiResponse<UserSessionsPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByUser(
        Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _sessions.ListByUserAsync(userId, page, pageSize, cancellationToken);
        return Ok(ApiResponse<UserSessionsPageDto>.Ok(result));
    }

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Policy = "perm:" + PlatformPermissions.AdminsManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(
        Guid id,
        [FromBody] RevokeSessionRequest? body,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("Non authentifié."));

        var reason = body?.Reason ?? "ManualRevoke";
        var ok = await _sessions.RevokeAsync(id, actorId, reason, cancellationToken);
        if (!ok)
            return NotFound(ApiResponse<object>.Fail("Session introuvable."));

        _logger.LogInformation("Platform admin {ActorId} revoked session {SessionId}: {Reason}",
            actorId, id, reason);
        return Ok(ApiResponse<object>.Ok(null!, "Session révoquée."));
    }

    [HttpPost("users/{userId:guid}/revoke-all")]
    [Authorize(Policy = "perm:" + PlatformPermissions.AdminsManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeAllByUser(
        Guid userId,
        [FromBody] RevokeAllSessionsRequest? body,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("Non authentifié."));

        var reason = body?.Reason ?? "AdminRevokeAll";
        var count = await _sessions.RevokeAllByUserAsync(userId, actorId, reason, cancellationToken);

        _logger.LogInformation("Platform admin {ActorId} revoked {Count} sessions of user {UserId}: {Reason}",
            actorId, count, userId, reason);

        return Ok(ApiResponse<object>.Ok(null!, $"{count} session(s) révoquée(s)."));
    }
}
