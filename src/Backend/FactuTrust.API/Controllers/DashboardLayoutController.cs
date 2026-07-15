using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.DashboardLayout;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Disposition personnalisée du tableau de bord (ordre des blocs), propre à chaque
/// utilisateur authentifié. Aucune permission spéciale : tout utilisateur peut
/// organiser son propre tableau de bord.
/// </summary>
[ApiController]
[Route("api/settings/dashboard-layout")]
[Authorize]
public sealed class DashboardLayoutController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardLayoutController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<DashboardLayoutDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var dto = await _mediator.Send(new GetMyDashboardLayoutQuery(), cancellationToken);
        return Ok(ApiResponse<DashboardLayoutDto>.Ok(dto));
    }

    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<DashboardLayoutDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Save(
        [FromBody] SaveDashboardLayoutRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SaveMyDashboardLayoutCommand(request), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<DashboardLayoutDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<DashboardLayoutDto>.Ok(result.Value));
    }
}
