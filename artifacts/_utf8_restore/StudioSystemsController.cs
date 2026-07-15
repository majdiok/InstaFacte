using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Systems;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Studio;

[ApiController]
[Route("api/studio/systems")]
[Authorize(Policy = PermissionPolicies.StudioDesignEntities)]
public sealed class StudioSystemsController : ControllerBase
{
    private readonly IMediator _mediator;

    public StudioSystemsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListCustomSystemsQuery(includeInactive), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<IReadOnlyList<CustomSystemDto>>.Ok(result.Value));
    }

    [HttpGet("{key}")]
    [Authorize(Policy = PermissionPolicies.CustomRecordsRead)]
    public async Task<IActionResult> GetByKey(string key, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCustomSystemByKeyQuery(key), cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<CustomSystemDetailDto>.Ok(result.Value));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomSystemRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateCustomSystemCommand(request), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<CustomSystemDto>.Ok(result.Value));
    }
}
