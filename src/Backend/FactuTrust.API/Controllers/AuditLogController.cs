using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize]
public sealed class AuditLogController : ControllerBase
{
    private readonly IAuditLogQueryService _auditLogs;

    public AuditLogController(IAuditLogQueryService auditLogs)
    {
        _auditLogs = auditLogs;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AuditRead)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? action,
        [FromQuery] Guid? userId,
        [FromQuery] string? entityType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var r = await _auditLogs.GetLogsAsync(from, to, action, userId, entityType, page, pageSize, cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<PagedResult<AuditLogEntryDto>>.Ok(r.Value));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.AuditRead)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var r = await _auditLogs.GetByIdAsync(id, cancellationToken);
        if (r.IsFailure)
        {
            if (r.Error.Code == $"{nameof(AuditLog)}.NotFound")
                return NotFound(ApiResponse<object>.Fail(r.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        }

        return Ok(ApiResponse<AuditLogDetailDto>.Ok(r.Value));
    }

    [HttpGet("verify-integrity")]
    [Authorize(Policy = PermissionPolicies.AuditRead)]
    public async Task<IActionResult> VerifyIntegrity(CancellationToken cancellationToken)
    {
        var r = await _auditLogs.VerifyChainAsync(cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<AuditChainVerificationDto>.Ok(r.Value));
    }
}
