using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Maintenance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Studio;

/// <summary>
/// Studio maintenance actions. Currently: (re)build the JSON computed-column indexes for unique
/// fields (backfill existing fields / recover from a prior best-effort failure). Design-time only.
/// </summary>
[ApiController]
[Route("api/studio/maintenance")]
[Authorize(Policy = PermissionPolicies.StudioDesignEntities)]
public sealed class StudioMaintenanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public StudioMaintenanceController(IMediator mediator) => _mediator = mediator;

    [HttpPost("rebuild-indexes")]
    public async Task<IActionResult> RebuildIndexes(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RebuildStudioIndexesCommand(), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<object>.Ok(new { indexedKeys = result.Value }, $"{result.Value} champ(s) unique(s) indexé(s)."));
    }
}
