using System.Net;
using FactuTrust.API.Services.Channels;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Features.AI;
using FactuTrust.Infrastructure.Services.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Callback loopback du sidecar Cursor (custom tools). Authentifié par jeton de run, pas par JWT.
/// Restaure le tenant et l'identité capturés sur la requête chat d'origine.
/// </summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/ai/cursor-tools")]
public sealed class CursorToolCallbackController : ControllerBase
{
    private readonly CursorToolCallbackService _callback;
    private readonly ICursorToolRunRegistry _registry;
    private readonly ITenantContext _tenantContext;

    public CursorToolCallbackController(
        CursorToolCallbackService callback,
        ICursorToolRunRegistry registry,
        ITenantContext tenantContext)
    {
        _callback = callback;
        _registry = registry;
        _tenantContext = tenantContext;
    }

    [HttpPost("{runId:guid}")]
    public async Task<IActionResult> Execute(
        Guid runId,
        [FromBody] CursorToolCallbackRequest request,
        CancellationToken cancellationToken)
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        if (remoteIp is not null && !IPAddress.IsLoopback(remoteIp))
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "loopback only" });

        var token = Request.Headers["X-Cursor-Run-Token"].ToString();
        if (!_registry.TryGet(runId, out var ctx) || !CursorToolRunRegistry.TokenEquals(ctx.Token, token))
            return Unauthorized(new { error = "unauthorized" });

        _tenantContext.SetTenant(ctx.TenantId, ctx.ConnectionString);
        ChannelUserContext.Set(new ChannelUserSnapshot(
            ctx.UserId, ctx.TenantId, ctx.Email, ctx.Role, ctx.Permissions));
        try
        {
            var (status, body) = await _callback.ExecuteAsync(runId, token, request, cancellationToken);
            return StatusCode(status, body);
        }
        finally
        {
            ChannelUserContext.Clear();
        }
    }
}
