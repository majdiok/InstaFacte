using System.Security.Claims;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/firm-assignments")]
[Authorize]
[Filters.RequireAccountingFirmsFeature]
public sealed class FirmClientAssignmentsController : ControllerBase
{
    private readonly IFirmAssignmentService _assignmentService;

    public FirmClientAssignmentsController(IFirmAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public async Task<ActionResult<ApiResponse<FirmClientAssignmentDto>>> RequestAssignment(
        [FromBody] RequestFirmAssignmentDto dto,
        CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null)
            return Unauthorized();

        var result = await _assignmentService.RequestAssignmentAsync(tenantId.Value, userId.Value, dto, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<FirmClientAssignmentDto>.Fail(result.Error.Description));

        return Ok(ApiResponse<FirmClientAssignmentDto>.Ok(result.Value, "Demande envoyée au cabinet"));
    }

    [HttpGet("company/current")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public async Task<ActionResult<ApiResponse<FirmClientAssignmentDto?>>> GetCompanyCurrent(CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var assignment = await _assignmentService.GetCompanyCurrentAssignmentAsync(tenantId.Value, cancellationToken);
        return Ok(ApiResponse<FirmClientAssignmentDto?>.Ok(assignment));
    }

    [HttpGet("company/history")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmClientAssignmentDto>>>> GetCompanyHistory(CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var history = await _assignmentService.GetCompanyAssignmentHistoryAsync(tenantId.Value, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FirmClientAssignmentDto>>.Ok(history));
    }

    [HttpDelete("company/current")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public async Task<ActionResult<ApiResponse<object>>> RevokeByCompany(CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null)
            return Unauthorized();

        var result = await _assignmentService.RevokeByCompanyAsync(tenantId.Value, userId.Value, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(null!, "Affectation révoquée"));
    }

    [HttpGet("firm/incoming")]
    [Authorize(Roles = $"{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmClientAssignmentDto>>>> GetIncoming(CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var items = await _assignmentService.GetIncomingInvitationsAsync(tenantId.Value, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FirmClientAssignmentDto>>.Ok(items));
    }

    [HttpGet("firm/clients")]
    [Authorize(Roles = $"{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmClientDossierDto>>>> GetActiveClients(CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var clients = await _assignmentService.GetActiveClientsAsync(tenantId.Value, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FirmClientDossierDto>>.Ok(clients));
    }

    [HttpPost("firm/{assignmentId:guid}/accept")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<object>>> Accept(Guid assignmentId, CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null)
            return Unauthorized();

        var result = await _assignmentService.AcceptAssignmentAsync(tenantId.Value, assignmentId, userId.Value, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(null!, "Demande acceptée"));
    }

    [HttpPost("firm/{assignmentId:guid}/reject")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<object>>> Reject(Guid assignmentId, CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null)
            return Unauthorized();

        var result = await _assignmentService.RejectAssignmentAsync(tenantId.Value, assignmentId, userId.Value, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(null!, "Demande refusée"));
    }

    [HttpDelete("firm/{assignmentId:guid}")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<object>>> RevokeByFirm(Guid assignmentId, CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null)
            return Unauthorized();

        var result = await _assignmentService.RevokeByFirmAsync(tenantId.Value, assignmentId, userId.Value, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(null!, "Dossier résilié"));
    }

    private Guid? GetHomeTenantId()
    {
        var claim = User.FindFirstValue("tenant_id");
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
