using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Systems;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Studio;

/// <summary>
/// Lightweight list of active custom entities for the dynamic sidebar section. Available to any
/// user who can read custom records, so the runtime entries appear for end users (not just designers).
/// </summary>
[ApiController]
[Route("api/studio/nav")]
[Authorize(Policy = PermissionPolicies.CustomRecordsRead)]
public sealed class StudioNavController : ControllerBase
{
    private readonly IMediator _mediator;

    public StudioNavController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetStudioNavQuery(), cancellationToken);
        if (result.IsFailure)
            return Ok(ApiResponse<IReadOnlyList<StudioNavNodeDto>>.Ok(Array.Empty<StudioNavNodeDto>()));

        return Ok(ApiResponse<IReadOnlyList<StudioNavNodeDto>>.Ok(result.Value));
    }
}
