using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.CRM;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CrmController : ControllerBase
{
    private readonly IMediator _mediator;
    public CrmController(IMediator mediator) => _mediator = mediator;

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string GetUserName() => User.FindFirstValue("firstName") ?? User.FindFirstValue(ClaimTypes.Name) ?? "Unknown";

    // ==================== OPPORTUNITIES ====================

    [HttpGet("opportunities")]
    [Authorize(Policy = PermissionPolicies.CrmRead)]
    public async Task<IActionResult> GetOpportunities(
        [FromQuery] int? stage, [FromQuery] Guid? assignedUserId, [FromQuery] Guid? clientId, CancellationToken ct)
    {
        var s = stage.HasValue ? (OpportunityStage?)stage.Value : null;
        var r = await _mediator.Send(new GetOpportunitiesQuery(s, assignedUserId, clientId), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<IReadOnlyList<OpportunityDto>>.Ok(r.Value));
    }

    [HttpGet("opportunities/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CrmRead)]
    public async Task<IActionResult> GetOpportunity(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetOpportunityByIdQuery(id), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<OpportunityDto>.Ok(r.Value));
    }

    [HttpPost("opportunities")]
    [Authorize(Policy = PermissionPolicies.CrmCreate)]
    public async Task<IActionResult> CreateOpportunity([FromBody] CreateOpportunityRequest request, CancellationToken ct)
    {
        var r = await _mediator.Send(new CreateOpportunityCommand(request, GetUserId(), GetUserName()), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(r.Value));
    }

    [HttpPut("opportunities/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CrmUpdate)]
    public async Task<IActionResult> UpdateOpportunity(Guid id, [FromBody] UpdateOpportunityRequest request, CancellationToken ct)
    {
        var r = await _mediator.Send(new UpdateOpportunityCommand(id, request), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPut("opportunities/{id:guid}/advance")]
    [Authorize(Policy = PermissionPolicies.CrmUpdate)]
    public async Task<IActionResult> AdvanceOpportunity(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new AdvanceOpportunityCommand(id), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPut("opportunities/{id:guid}/win")]
    [Authorize(Policy = PermissionPolicies.CrmUpdate)]
    public async Task<IActionResult> WinOpportunity(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new WinOpportunityCommand(id), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPut("opportunities/{id:guid}/lose")]
    [Authorize(Policy = PermissionPolicies.CrmUpdate)]
    public async Task<IActionResult> LoseOpportunity(Guid id, [FromBody] LoseOpportunityRequest request, CancellationToken ct)
    {
        var r = await _mediator.Send(new LoseOpportunityCommand(id, request.Reason), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpDelete("opportunities/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CrmDelete)]
    public async Task<IActionResult> DeleteOpportunity(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new DeleteOpportunityCommand(id), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<bool>.Ok(true));
    }

    // ==================== ACTIVITIES ====================

    [HttpGet("assignable-users")]
    [Authorize(Policy = PermissionPolicies.CrmRead)]
    public async Task<IActionResult> GetCrmAssignableUsers(CancellationToken ct)
    {
        var r = await _mediator.Send(new GetCrmAssignableUsersQuery(), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<IReadOnlyList<CrmAssignableUserDto>>.Ok(r.Value));
    }

    [HttpGet("activities")]
    [Authorize(Policy = PermissionPolicies.CrmRead)]
    public async Task<IActionResult> GetActivities(
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? assignedUserId,
        [FromQuery] Guid? opportunityId,
        [FromQuery] bool? completed,
        [FromQuery] DateTime? dueFrom,
        [FromQuery] DateTime? dueTo,
        [FromQuery] int? activityType,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var r = await _mediator.Send(
            new GetActivitiesQuery(clientId, assignedUserId, opportunityId, completed, dueFrom, dueTo, activityType, search, page, pageSize),
            ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<PagedResult<SalesActivityDto>>.Ok(r.Value));
    }

    [HttpGet("activities/summary")]
    [Authorize(Policy = PermissionPolicies.CrmRead)]
    public async Task<IActionResult> GetActivitiesSummary(
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? assignedUserId,
        [FromQuery] Guid? opportunityId,
        [FromQuery] bool? completed,
        [FromQuery] DateTime? dueFrom,
        [FromQuery] DateTime? dueTo,
        [FromQuery] int? activityType,
        [FromQuery] string? search,
        CancellationToken ct = default)
    {
        var r = await _mediator.Send(
            new GetActivitiesSummaryQuery(clientId, assignedUserId, opportunityId, completed, dueFrom, dueTo, activityType, search),
            ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<ActivityListSummaryDto>.Ok(r.Value));
    }

    [HttpGet("activities/my-reminders")]
    [Authorize(Policy = PermissionPolicies.CrmRead)]
    public async Task<IActionResult> GetMyReminders(CancellationToken ct)
    {
        var r = await _mediator.Send(new GetMyRemindersQuery(GetUserId()), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<IReadOnlyList<SalesActivityDto>>.Ok(r.Value));
    }

    [HttpPost("activities")]
    [Authorize(Policy = PermissionPolicies.CrmCreate)]
    public async Task<IActionResult> CreateActivity([FromBody] CreateActivityRequest request, CancellationToken ct)
    {
        var r = await _mediator.Send(new CreateActivityCommand(request, GetUserId(), GetUserName()), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(r.Value));
    }

    [HttpPut("activities/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CrmUpdate)]
    public async Task<IActionResult> UpdateActivity(Guid id, [FromBody] UpdateActivityRequest request, CancellationToken ct)
    {
        var r = await _mediator.Send(new UpdateActivityCommand(id, request), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPut("activities/{id:guid}/complete")]
    [Authorize(Policy = PermissionPolicies.CrmUpdate)]
    public async Task<IActionResult> CompleteActivity(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new CompleteActivityCommand(id), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpDelete("activities/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CrmDelete)]
    public async Task<IActionResult> DeleteActivity(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new DeleteActivityCommand(id), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<bool>.Ok(true));
    }

    // ==================== SALES TARGETS ====================

    [HttpGet("targets/assignable-users")]
    [Authorize(Policy = PermissionPolicies.SalesTargetsRead)]
    public async Task<IActionResult> GetTargetsAssignableUsers(CancellationToken ct)
    {
        var r = await _mediator.Send(new GetCrmAssignableUsersQuery(), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<IReadOnlyList<CrmAssignableUserDto>>.Ok(r.Value));
    }

    [HttpGet("targets")]
    [Authorize(Policy = PermissionPolicies.SalesTargetsRead)]
    public async Task<IActionResult> GetTargets([FromQuery] int? year, [FromQuery] Guid? userId, [FromQuery] int? month, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetSalesTargetsQuery(year ?? DateTime.UtcNow.Year, userId, month), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<IReadOnlyList<SalesTargetDto>>.Ok(r.Value));
    }

    [HttpPost("targets")]
    [Authorize(Policy = PermissionPolicies.SalesTargetsManage)]
    public async Task<IActionResult> CreateTarget([FromBody] CreateSalesTargetRequest request, CancellationToken ct)
    {
        var r = await _mediator.Send(new CreateSalesTargetCommand(request), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(r.Value));
    }

    [HttpPut("targets/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SalesTargetsManage)]
    public async Task<IActionResult> UpdateTarget(Guid id, [FromBody] CreateSalesTargetRequest request, CancellationToken ct)
    {
        var r = await _mediator.Send(new UpdateSalesTargetCommand(id, request.TargetAmount), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpDelete("targets/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.SalesTargetsManage)]
    public async Task<IActionResult> DeleteTarget(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new DeleteSalesTargetCommand(id), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<bool>.Ok(true));
    }

    // ==================== QUOTE TEMPLATES ====================

    [HttpGet("quote-templates")]
    [Authorize(Policy = PermissionPolicies.CrmRead)]
    public async Task<IActionResult> GetQuoteTemplates([FromQuery] bool? activeOnly, [FromQuery] string? search, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetQuoteTemplatesQuery(activeOnly, search), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<IReadOnlyList<QuoteTemplateDto>>.Ok(r.Value));
    }

    [HttpGet("quote-templates/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CrmRead)]
    public async Task<IActionResult> GetQuoteTemplate(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetQuoteTemplateByIdQuery(id), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<QuoteTemplateDto>.Ok(r.Value));
    }

    [HttpPost("quote-templates")]
    [Authorize(Policy = PermissionPolicies.CrmCreate)]
    public async Task<IActionResult> CreateQuoteTemplate([FromBody] CreateQuoteTemplateRequest request, CancellationToken ct)
    {
        var r = await _mediator.Send(new CreateQuoteTemplateCommand(request), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(r.Value));
    }

    [HttpPut("quote-templates/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CrmUpdate)]
    public async Task<IActionResult> UpdateQuoteTemplate(Guid id, [FromBody] UpdateQuoteTemplateRequest request, CancellationToken ct)
    {
        var r = await _mediator.Send(new UpdateQuoteTemplateCommand(id, request, GetUserName()), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPatch("quote-templates/{id:guid}/active")]
    [Authorize(Policy = PermissionPolicies.CrmUpdate)]
    public async Task<IActionResult> SetQuoteTemplateActive(Guid id, [FromBody] SetQuoteTemplateActiveRequest request, CancellationToken ct)
    {
        var r = await _mediator.Send(new SetQuoteTemplateActiveCommand(id, request.IsActive, GetUserName()), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpDelete("quote-templates/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.CrmDelete)]
    public async Task<IActionResult> DeleteQuoteTemplate(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new DeleteQuoteTemplateCommand(id), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<bool>.Ok(true));
    }
}
