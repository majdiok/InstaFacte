using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Lot B4 — Endpoints sécurité plateforme : tentatives login échouées + brute-force detection.
/// </summary>
[ApiController]
[Route("api/platform/security")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformSecurityController : ControllerBase
{
    private readonly IFailedLoginAttemptService _failedLogins;

    public PlatformSecurityController(IFailedLoginAttemptService failedLogins)
    {
        _failedLogins = failedLogins;
    }

    /// <summary>Liste paginée des tentatives échouées avec KPIs (24h/1h/top IP suspectes).</summary>
    [HttpGet("failed-logins")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SecurityRead)]
    [ProducesResponseType(typeof(ApiResponse<FailedLoginAttemptsPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFailedLogins(
        [FromQuery] string? email = null,
        [FromQuery] string? ipAddress = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _failedLogins.ListAsync(email, ipAddress, from, to, page, pageSize, cancellationToken);
        return Ok(ApiResponse<FailedLoginAttemptsPageDto>.Ok(result));
    }
}
