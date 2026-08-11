using System.Security.Claims;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/firm/payroll")]
[Authorize(Roles = nameof(UserRole.FirmManager))]
[Filters.RequireAccountingFirmsFeature]
public sealed class FirmInternalPayrollController : ControllerBase
{
    private readonly IFirmInternalPayrollProvisioningService _provisioning;
    private readonly IFirmGovernanceFeature _feature;

    public FirmInternalPayrollController(
        IFirmInternalPayrollProvisioningService provisioning,
        IFirmGovernanceFeature feature)
    {
        _provisioning = provisioning;
        _feature = feature;
    }

    [HttpGet("provisioning-status")]
    public async Task<ActionResult<ApiResponse<FirmPayrollProvisioningStatusDto>>> GetProvisioningStatus(
        CancellationToken cancellationToken)
    {
        if (!EnsureInternalPayroll(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var status = await _provisioning.GetProvisioningStatusAsync(tenantId.Value, cancellationToken);
        return Ok(ApiResponse<FirmPayrollProvisioningStatusDto>.Ok(status));
    }

    [HttpPost("provision-from-collaborators")]
    public async Task<ActionResult<ApiResponse<FirmPayrollProvisionResultDto>>> ProvisionFromCollaborators(
        CancellationToken cancellationToken)
    {
        if (!EnsureInternalPayroll(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _provisioning.ProvisionFromCollaboratorsAsync(
            tenantId.Value, isManager: true, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<FirmPayrollProvisionResultDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmPayrollProvisionResultDto>.Ok(
            result.Value,
            $"{result.Value.Created} créé(s), {result.Value.Linked} lié(s)."));
    }

    [HttpPost("provision/{collaboratorUserId:guid}")]
    public async Task<ActionResult<ApiResponse<FirmPayrollProvisionResultDto>>> ProvisionCollaborator(
        Guid collaboratorUserId,
        CancellationToken cancellationToken)
    {
        if (!EnsureInternalPayroll(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _provisioning.ProvisionCollaboratorAsync(
            tenantId.Value, isManager: true, collaboratorUserId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<FirmPayrollProvisionResultDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmPayrollProvisionResultDto>.Ok(result.Value));
    }

    private bool EnsureInternalPayroll(out ActionResult? disabled)
    {
        if (!_feature.IsEnabled || !_feature.IsInternalPayrollEnabled)
        {
            disabled = BadRequest(ApiResponse<object>.Fail(
                "La paie interne du cabinet n'est pas activée."));
            return false;
        }

        disabled = null;
        return true;
    }

    private Guid? GetHomeTenantId()
    {
        var claim = User.FindFirstValue("tenant_id");
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
