using System.Security.Claims;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Endpoint lecture des comptables cabinet affectables (gestionnaires comptables).
/// Contrôleur séparé pour autoriser FirmManager et FirmAccountant sans policy users:manage.
/// </summary>
[ApiController]
[Route("api/firm/users")]
[Authorize(Roles = $"{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
[Filters.RequireAccountingFirmsFeature]
public sealed class FirmAssignableUsersController : ControllerBase
{
    private readonly IFirmGovernanceService _governance;
    private readonly IFirmGovernanceFeature _feature;

    public FirmAssignableUsersController(IFirmGovernanceService governance, IFirmGovernanceFeature feature)
    {
        _governance = governance;
        _feature = feature;
    }

    [HttpGet("assignable")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmAssignableAccountantDto>>>> ListAssignable(
        CancellationToken cancellationToken)
    {
        if (!_feature.IsEnabled)
            return StatusCode(503, ApiResponse<object>.Fail("Module gouvernance cabinet désactivé."));

        var claim = User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(claim, out var tenantId))
            return Unauthorized();

        var list = await _governance.ListAssignableAccountantsAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FirmAssignableAccountantDto>>.Ok(list));
    }
}
