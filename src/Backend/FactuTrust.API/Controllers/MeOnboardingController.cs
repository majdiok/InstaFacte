using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// First-login product tour and « Premiers pas » checklist for the authenticated user (Master DB).
/// No extra permission: every interactive user owns their own onboarding state.
/// </summary>
[ApiController]
[Route("api/me/onboarding")]
[Authorize]
public sealed class MeOnboardingController : ControllerBase
{
    private readonly IProductOnboardingService _onboarding;

    public MeOnboardingController(IProductOnboardingService onboarding) => _onboarding = onboarding;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ProductOnboardingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var dto = await _onboarding.GetMineAsync(cancellationToken);
        return Ok(ApiResponse<ProductOnboardingDto>.Ok(dto));
    }

    [HttpPatch]
    [ProducesResponseType(typeof(ApiResponse<ProductOnboardingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Patch(
        [FromBody] PatchProductOnboardingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _onboarding.PatchMineAsync(request ?? new PatchProductOnboardingRequest(), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<ProductOnboardingDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<ProductOnboardingDto>.Ok(result.Value));
    }
}
