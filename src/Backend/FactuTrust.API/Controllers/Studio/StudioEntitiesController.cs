using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Studio;

/// <summary>
/// Design-time management of custom entities ("tables") in the low-code Studio.
/// Requires the <c>studio:design_entities</c> permission (Developer, Administrator, Supervisor).
/// </summary>
[ApiController]
[Route("api/studio/entities")]
[Authorize(Policy = PermissionPolicies.StudioDesignEntities)]
public sealed class StudioEntitiesController : ControllerBase
{
    private readonly IMediator _mediator;

    public StudioEntitiesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListCustomEntitiesQuery(includeInactive), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<IReadOnlyList<CustomEntityDto>>.Ok(result.Value));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCustomEntityByIdQuery(id), cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<CustomEntityDto>.Ok(result.Value));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomEntityRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateCustomEntityCommand(request), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<CustomEntityDto>.Ok(result.Value));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomEntityRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateCustomEntityCommand(id, request), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<CustomEntityDto>.Ok(result.Value));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteCustomEntityCommand(id), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<object>.Ok(new { }, "Table supprimée."));
    }
}
