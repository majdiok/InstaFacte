using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Fields;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Studio;

/// <summary>
/// Design-time management of the fields of a custom entity. Requires <c>studio:design_entities</c>.
/// </summary>
[ApiController]
[Route("api/studio/entities/{entityId:guid}/fields")]
[Authorize(Policy = PermissionPolicies.StudioDesignEntities)]
public sealed class StudioFieldsController : ControllerBase
{
    private readonly IMediator _mediator;

    public StudioFieldsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List(Guid entityId, [FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListCustomFieldsQuery(entityId, includeInactive), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<IReadOnlyList<CustomFieldDto>>.Ok(result.Value));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid entityId, [FromBody] CreateCustomFieldRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateCustomFieldCommand(entityId, request), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<CustomFieldDto>.Ok(result.Value));
    }

    [HttpPut("{fieldId:guid}")]
    public async Task<IActionResult> Update(Guid entityId, Guid fieldId, [FromBody] UpdateCustomFieldRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateCustomFieldCommand(fieldId, request), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<CustomFieldDto>.Ok(result.Value));
    }

    [HttpDelete("{fieldId:guid}")]
    public async Task<IActionResult> Delete(Guid entityId, Guid fieldId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteCustomFieldCommand(fieldId), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<object>.Ok(new { }, "Champ supprimé."));
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder(Guid entityId, [FromBody] ReorderCustomFieldsRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ReorderCustomFieldsCommand(entityId, request), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<object>.Ok(new { }, "Ordre mis à jour."));
    }
}
