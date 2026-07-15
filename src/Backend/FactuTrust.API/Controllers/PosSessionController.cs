using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Persists optional POS cart state server-side (per user) for sync across devices.
/// In-memory store — sufficient for dev / single-instance; replace with EF/Redis for scale-out.
/// </summary>
[ApiController]
[Route("api/pos")]
[Authorize]
public sealed class PosSessionController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, string> Sessions = new();

    public sealed class PosSessionStateDto
    {
        public JsonElement? State { get; set; }
    }

    public sealed class PosSessionSaveRequest
    {
        public JsonElement State { get; set; }
    }

    private string SessionKey()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return id ?? "";
    }

    [HttpGet("session")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<PosSessionStateDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<PosSessionStateDto>> GetSession()
    {
        var key = SessionKey();
        if (string.IsNullOrEmpty(key))
            return Unauthorized(ApiResponse<PosSessionStateDto>.Fail("Utilisateur non identifié"));

        if (!Sessions.TryGetValue(key, out var json))
            return Ok(ApiResponse<PosSessionStateDto>.Ok(new PosSessionStateDto { State = null }));

        using var doc = JsonDocument.Parse(json);
        return Ok(ApiResponse<PosSessionStateDto>.Ok(new PosSessionStateDto { State = doc.RootElement.Clone() }));
    }

    [HttpPost("session")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<object?>> SaveSession([FromBody] PosSessionSaveRequest? body)
    {
        var key = SessionKey();
        if (string.IsNullOrEmpty(key))
            return Unauthorized(ApiResponse<object?>.Fail("Utilisateur non identifié"));

        if (body is null || body.State.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return BadRequest(ApiResponse<object?>.Fail("État POS manquant"));

        Sessions[key] = body.State.GetRawText();
        return Ok(ApiResponse<object?>.Ok(null));
    }

    [HttpPost("session/clear")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<object?>> ClearSession()
    {
        var key = SessionKey();
        if (!string.IsNullOrEmpty(key))
            Sessions.TryRemove(key, out _);
        return Ok(ApiResponse<object?>.Ok(null));
    }
}
