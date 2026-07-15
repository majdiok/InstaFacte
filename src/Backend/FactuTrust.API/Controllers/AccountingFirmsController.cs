using System.Security.Claims;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/accounting-firms")]
[Authorize]
[Filters.RequireAccountingFirmsFeature]
public sealed class AccountingFirmsController : ControllerBase
{
    private readonly IFirmAssignmentService _assignmentService;

    public AccountingFirmsController(IFirmAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    [HttpGet("directory")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AccountingFirmDirectoryItemDto>>>> GetDirectory(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var items = await _assignmentService.SearchDirectoryAsync(search, page, pageSize, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AccountingFirmDirectoryItemDto>>.Ok(items));
    }

    [HttpGet("me/profile")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<AccountingFirmProfileDto>>> GetMyProfile(CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var profile = await _assignmentService.GetFirmProfileAsync(tenantId.Value, cancellationToken);
        if (profile is null)
            return NotFound(ApiResponse<AccountingFirmProfileDto>.Fail("Profil cabinet introuvable"));

        return Ok(ApiResponse<AccountingFirmProfileDto>.Ok(profile));
    }

    [HttpPut("me/profile")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<object>>> UpdateMyProfile(
        [FromBody] UpdateAccountingFirmProfileDto dto,
        CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var result = await _assignmentService.UpdateFirmProfileAsync(tenantId.Value, dto, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return Ok(ApiResponse<object>.Ok(null!, "Profil mis à jour"));
    }

    private Guid? GetHomeTenantId()
    {
        var claim = User.FindFirstValue("tenant_id");
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
