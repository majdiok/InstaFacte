using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Forms;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Studio;

/// <summary>
/// Design-time management of an entity's default form layout. Requires <c>studio:design_forms</c>.
/// </summary>
[ApiController]
[Route("api/studio/entities/{entityId:guid}/form")]
[Authorize(Policy = PermissionPolicies.StudioDesignForms)]
public sealed class StudioFormsController : ControllerBase
{
    private readonly IMediator _mediator;

    public StudioFormsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Get(Guid entityId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCustomFormQuery(entityId), cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<CustomFormDto>.Ok(result.Value));
    }

    [HttpPut]
    public async Task<IActionResult> Upsert(Guid entityId, [FromBody] SaveFormLayoutRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpsertDefaultFormCommand(entityId, request), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<CustomFormDto>.Ok(result.Value));
    }
}
