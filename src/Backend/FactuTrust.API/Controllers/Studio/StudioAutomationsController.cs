using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Automations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Studio;

/// <summary>
/// Studio "ERP bridge" automations: declarative bindings from a custom entity's records to validated ERP
/// actions. Design (list/save/delete + action catalogue) requires <c>studio:design_entities</c>; running an
/// automation on a record requires <c>custom_records:write</c> (and the action's own permission, enforced by
/// the tool executor). Reading run history requires <c>custom_records:read</c>.
/// </summary>
[ApiController]
[Route("api/studio")]
[Authorize]
public sealed class StudioAutomationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public StudioAutomationsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Catalogue of ERP actions (mutating tools) usable as automations, with their parameters.</summary>
    [HttpGet("automations/actions")]
    [Authorize(Policy = PermissionPolicies.StudioDesignEntities)]
    public async Task<IActionResult> ListActions(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListAutomationActionsQuery(), ct);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<IReadOnlyList<AutomationActionDto>>.Ok(result.Value));
    }

    [HttpGet("entities/{entityId:guid}/automations")]
    [Authorize(Policy = PermissionPolicies.StudioDesignEntities)]
    public async Task<IActionResult> List(Guid entityId, CancellationToken ct)
    {
        var result = await _mediator.Send(new ListAutomationsQuery(entityId), ct);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<IReadOnlyList<AutomationDto>>.Ok(result.Value));
    }

    [HttpPost("entities/{entityId:guid}/automations")]
    [Authorize(Policy = PermissionPolicies.StudioDesignEntities)]
    public async Task<IActionResult> Create(Guid entityId, [FromBody] SaveAutomationRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpsertAutomationCommand(entityId, null, request), ct);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<AutomationDto>.Ok(result.Value));
    }

    [HttpPut("entities/{entityId:guid}/automations/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.StudioDesignEntities)]
    public async Task<IActionResult> Update(Guid entityId, Guid id, [FromBody] SaveAutomationRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpsertAutomationCommand(entityId, id, request), ct);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<AutomationDto>.Ok(result.Value));
    }

    [HttpDelete("automations/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.StudioDesignEntities)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteAutomationCommand(id), ct);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<object>.Ok(new { }));
    }

    /// <summary>Runs one automation on one record on demand (manual trigger).</summary>
    [HttpPost("records/{entityKey}/{recordId:guid}/automations/{automationId:guid}/run")]
    [Authorize(Policy = PermissionPolicies.CustomRecordsWrite)]
    public async Task<IActionResult> Run(string entityKey, Guid recordId, Guid automationId, CancellationToken ct)
    {
        var result = await _mediator.Send(new RunAutomationCommand(entityKey, recordId, automationId), ct);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<AutomationRunDto>.Ok(result.Value));
    }

    [HttpGet("records/{entityKey}/{recordId:guid}/automation-runs")]
    [Authorize(Policy = PermissionPolicies.CustomRecordsRead)]
    public async Task<IActionResult> RunsForRecord(string entityKey, Guid recordId, CancellationToken ct)
    {
        var result = await _mediator.Send(new ListRecordAutomationRunsQuery(entityKey, recordId), ct);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<IReadOnlyList<AutomationRunDto>>.Ok(result.Value));
    }
}
