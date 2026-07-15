using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Views;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Studio;

/// <summary>
/// READ-ONLY views over existing tenant tables. Design (list/get/save/delete) requires
/// <c>studio:design_forms</c>; running a view requires <c>custom_reports:view</c>. Views never write.
/// </summary>
[ApiController]
[Route("api/studio/views")]
[Authorize]
public sealed class StudioViewsController : ControllerBase
{
    private readonly IMediator _mediator;

    public StudioViewsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.StudioDesignForms)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListCustomViewsQuery(), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<IReadOnlyList<CustomViewDto>>.Ok(result.Value));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.StudioDesignForms)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCustomViewQuery(id), cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<CustomViewDto>.Ok(result.Value));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.StudioDesignForms)]
    public async Task<IActionResult> Create([FromBody] SaveCustomViewRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpsertCustomViewCommand(null, request), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<CustomViewDto>.Ok(result.Value));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.StudioDesignForms)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveCustomViewRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpsertCustomViewCommand(id, request), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<CustomViewDto>.Ok(result.Value));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.StudioDesignForms)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteCustomViewCommand(id), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<object>.Ok(new { }, "Vue supprimée."));
    }

    [HttpGet("{id:guid}/run")]
    [Authorize(Policy = PermissionPolicies.CustomReportsView)]
    public async Task<IActionResult> Run(Guid id, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new RunCustomViewQuery(id, search, page, pageSize), cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<SqlQueryResultDto>.Ok(result.Value));
    }
}
