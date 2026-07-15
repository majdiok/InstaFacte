using System.Security.Claims;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/firm/dashboard")]
[Authorize(Roles = $"{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
[Filters.RequireAccountingFirmsFeature]
public sealed class FirmDashboardController : ControllerBase
{
    private readonly IFirmDashboardService _dashboardService;

    public FirmDashboardController(IFirmDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<FirmDashboardDto>>> GetDashboard(CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var dashboard = await _dashboardService.GetDashboardAsync(tenantId.Value, cancellationToken);
        return Ok(ApiResponse<FirmDashboardDto>.Ok(dashboard));
    }

    private Guid? GetHomeTenantId()
    {
        var claim = User.FindFirstValue("tenant_id");
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
