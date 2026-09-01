using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.API.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Plan §3.3 — explainable, rule-based module usage recommendations for the current tenant.
/// Read-only nudges: nothing here changes module grants by itself — the frontend still drives the
/// change through <c>PUT /api/company/modules</c> (<see cref="CompanyModulesController"/>).
/// Dismissing a recommendation is permanent for that tenant/module pair.
/// </summary>
[ApiController]
[Route("api/company/module-recommendations")]
[Authorize]
public sealed class CompanyModuleRecommendationsController : ControllerBase
{
    private readonly IModuleUsageRecommendationService _recommendationService;

    public CompanyModuleRecommendationsController(IModuleUsageRecommendationService recommendationService)
    {
        _recommendationService = recommendationService;
    }

    /// <summary>Current module recommendations for the tenant (never includes dismissed or already-enabled modules).</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SettingsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ModuleRecommendationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecommendations(CancellationToken cancellationToken)
    {
        var recommendations = await _recommendationService.GetRecommendationsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ModuleRecommendationDto>>.Ok(recommendations));
    }

    /// <summary>Dismisses a recommendation for the current tenant — it will never resurface for this module.</summary>
    [HttpPost("{moduleId:int}/dismiss")]
    [Authorize(Policy = PermissionPolicies.SettingsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Dismiss(int moduleId, CancellationToken cancellationToken)
    {
        var result = await _recommendationService.DismissAsync(moduleId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code == "Unauthorized"
                ? Unauthorized(ApiResponse<bool>.Fail(result.Error.Description))
                : BadRequest(ApiResponse<bool>.Fail(result.Error.Description));
        }

        return Ok(ApiResponse<bool>.Ok(true, "Recommandation ignorée."));
    }
}
