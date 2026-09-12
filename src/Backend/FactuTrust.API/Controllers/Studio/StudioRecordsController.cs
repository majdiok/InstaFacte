using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Fields;
using FactuTrust.Application.Features.Studio.Records;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Studio;

/// <summary>
/// Runtime CRUD over the records of a custom entity, resolved by entity key. Reading requires
/// <c>custom_records:read</c>; writing requires <c>custom_records:write</c>. These are end-user
/// permissions (separate from the design-time studio:* permissions).
/// </summary>
[ApiController]
[Route("api/studio/records/{entityKey}")]
[Authorize]
public sealed class StudioRecordsController : ControllerBase
{
    private readonly IMediator _mediator;

    public StudioRecordsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Entity + active fields, for the runtime form/table renderer.</summary>
    [HttpGet("schema")]
    [Authorize(Policy = PermissionPolicies.CustomRecordsRead)]
    public async Task<IActionResult> Schema(string entityKey, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCustomEntitySchemaQuery(entityKey), cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<CustomEntitySchemaDto>.Ok(result.Value));
    }

    /// <summary>
    /// Paged list. <paramref name="filterField"/>/<paramref name="filterValue"/> (PR 2.1) add an exact
    /// server-side filter on one active field (e.g. junction rows of the current record for the « Liés »
    /// tab); cumulative with <paramref name="search"/>. <paramref name="pageSize"/> is clamped to 1..200.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.CustomRecordsRead)]
    public async Task<IActionResult> List(
        string entityKey,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? filterField = null,
        [FromQuery] string? filterValue = null,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        var result = await _mediator.Send(
            new ListCustomRecordsQuery(entityKey, search, page, pageSize, filterField, filterValue), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<PagedResult<CustomRecordDto>>.Ok(result.Value));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CustomRecordsRead)]
    public async Task<IActionResult> Get(string entityKey, Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCustomRecordQuery(entityKey, id), cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<CustomRecordDto>.Ok(result.Value));
    }

    /// <summary>Create. <c>record.duplicate_link</c> (junction pair already linked) ⇒ 409 ; any other error ⇒ 400 (historical).</summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.CustomRecordsWrite)]
    public async Task<IActionResult> Create(string entityKey, [FromBody] SaveCustomRecordRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateCustomRecordCommand(entityKey, request), cancellationToken);
        return result.IsFailure
            ? MapWriteError(result.Error)
            : Ok(ApiResponse<CustomRecordDto>.Ok(result.Value));
    }

    /// <summary>Update. <c>record.duplicate_link</c> ⇒ 409 ; any other error ⇒ 400 (historical).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CustomRecordsWrite)]
    public async Task<IActionResult> Update(string entityKey, Guid id, [FromBody] SaveCustomRecordRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateCustomRecordCommand(entityKey, id, request), cancellationToken);
        return result.IsFailure
            ? MapWriteError(result.Error)
            : Ok(ApiResponse<CustomRecordDto>.Ok(result.Value));
    }

    /// <summary>
    /// PR 2.1 : seul <see cref="StudioErrorCodes.RecordDuplicateLink"/> passe par <see cref="StudioErrorMapping"/>
    /// (409) ; tous les autres codes conservent le 400 historique de ce contrôleur (y compris <c>Conflict</c>
    /// d'unicité de champ et <c>*.NotFound</c>, pour ne pas changer le contrat existant du frontend).
    /// </summary>
    private IActionResult MapWriteError(Error error) =>
        error.Code == StudioErrorCodes.RecordDuplicateLink
            ? StudioErrorMapping.Map(this, error)
            : BadRequest(ApiResponse<object>.Fail(error.Description, error.Code));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CustomRecordsWrite)]
    public async Task<IActionResult> Delete(string entityKey, Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteCustomRecordCommand(entityKey, id), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<object>.Ok(new { }, "Enregistrement supprimé."));
    }
}
