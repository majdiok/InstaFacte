using System.Security.Claims;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/firm/context")]
[Authorize(Roles = $"{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
[Filters.RequireAccountingFirmsFeature]
public sealed class FirmContextController : ControllerBase
{
    private readonly IFirmContextService _contextService;
    private readonly UserManager<ApplicationUser> _userManager;

    public FirmContextController(
        IFirmContextService contextService,
        UserManager<ApplicationUser> userManager)
    {
        _contextService = contextService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<FirmContextDto>>> GetContext(CancellationToken cancellationToken)
    {
        var context = await _contextService.GetCurrentContextAsync(User, cancellationToken);
        return Ok(ApiResponse<FirmContextDto>.Ok(context));
    }

    [HttpPost("switch")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Switch(
        [FromBody] SwitchFirmContextDto dto,
        CancellationToken cancellationToken)
    {
        var (userId, tenantId) = await LoadUserContextAsync(cancellationToken);
        if (userId is null || tenantId is null)
            return Unauthorized();

        try
        {
            var tokens = await _contextService.SwitchToClientAsync(userId.Value, tenantId.Value, dto.ClientTenantId, cancellationToken);
            return Ok(ApiResponse<AuthResponseDto>.Ok(tokens, "Contexte dossier activé"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<AuthResponseDto>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<AuthResponseDto>.Fail(ex.Message));
        }
    }

    [HttpPost("clear")]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Clear(CancellationToken cancellationToken)
    {
        var (userId, tenantId) = await LoadUserContextAsync(cancellationToken);
        if (userId is null || tenantId is null)
            return Unauthorized();

        var tokens = await _contextService.ClearContextAsync(userId.Value, tenantId.Value, cancellationToken);
        return Ok(ApiResponse<AuthResponseDto>.Ok(tokens, "Contexte cabinet restauré"));
    }

    private async Task<(Guid? UserId, Guid? TenantId)> LoadUserContextAsync(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
            return (null, null);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return (null, null);

        return (user.Id, user.TenantId);
    }
}
