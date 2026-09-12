using FactuTrust.API.Authorization;
using FactuTrust.API.Http;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/timesheets")]
[Authorize]
public sealed class TimesheetsController : ControllerBase
{
    private readonly ITimesheetService _service;
    private readonly ProjectsOptions _options;

    public TimesheetsController(ITimesheetService service, IOptions<ProjectsOptions> options)
    {
        _service = service;
        _options = options.Value;
    }

    private ActionResult? GuardEnabled()
    {
        if (!_options.Enabled)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail(
                "Le module Projets/Timesheets est désactivé."));
        return null;
    }

    [HttpGet("settings")]
    [Authorize(Policy = PermissionPolicies.ProjectsRead)]
    public async Task<ActionResult<ApiResponse<TenantTimesheetSettingsDto>>> GetSettings(CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        return Ok(ApiResponse<TenantTimesheetSettingsDto>.Ok(await _service.GetSettingsAsync(cancellationToken)));
    }

    [HttpPut("settings")]
    [Authorize(Policy = PermissionPolicies.ProjectsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateSettings(
        [FromBody] UpdateTenantTimesheetSettingsDto dto, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.UpdateSettingsAsync(dto, cancellationToken);
        return result.IsSuccess ? Ok(ApiResponse<object>.Ok(null!)) : BadRequest(ApiResponse<object>.Fail(result.Error.Description));
    }

    [HttpGet("grid")]
    [Authorize(Policy = PermissionPolicies.ProjectTimeRead)]
    public async Task<ActionResult<ApiResponse<TimesheetGridDto>>> Grid(
        [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var grid = await _service.GetGridAsync(new TimesheetGridQuery { From = from, To = to, UserId = userId }, cancellationToken);
        return Ok(ApiResponse<TimesheetGridDto>.Ok(grid));
    }

    [HttpGet("kpi")]
    [Authorize(Policy = PermissionPolicies.ProjectTimeRead)]
    public async Task<ActionResult<ApiResponse<TimesheetBillingRateKpiDto>>> Kpi(
        [FromQuery] int year, [FromQuery] int month, [FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        return Ok(ApiResponse<TimesheetBillingRateKpiDto>.Ok(await _service.GetBillingRateKpiAsync(year, month, userId, cancellationToken)));
    }

    [HttpGet("leaderboard")]
    [Authorize(Policy = PermissionPolicies.ProjectTimeRead)]
    public async Task<ActionResult<ApiResponse<TimesheetLeaderboardDto>>> Leaderboard(
        [FromQuery] int year, [FromQuery] int month, [FromQuery] string mode = "billingRate", CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } guard) return guard;
        return Ok(ApiResponse<TimesheetLeaderboardDto>.Ok(await _service.GetLeaderboardAsync(year, month, mode, cancellationToken)));
    }

    [HttpGet("targets")]
    [Authorize(Policy = PermissionPolicies.ProjectsRead)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EmployeeBillingTimeTargetDto>>>> Targets(
        [FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        return Ok(ApiResponse<IReadOnlyList<EmployeeBillingTimeTargetDto>>.Ok(await _service.ListTargetsAsync(year, month, cancellationToken)));
    }

    [HttpPut("targets")]
    [Authorize(Policy = PermissionPolicies.ProjectsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpsertTarget(
        [FromBody] UpsertEmployeeBillingTimeTargetDto dto, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.UpsertTargetAsync(dto, cancellationToken);
        return result.IsSuccess ? Ok(ApiResponse<object>.Ok(null!)) : BadRequest(ApiResponse<object>.Fail(result.Error.Description));
    }

    [HttpGet("tips")]
    [Authorize(Policy = PermissionPolicies.ProjectsRead)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TimesheetTipDto>>>> Tips(CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        return Ok(ApiResponse<IReadOnlyList<TimesheetTipDto>>.Ok(await _service.ListTipsAsync(cancellationToken)));
    }

    [HttpPost("tips")]
    [Authorize(Policy = PermissionPolicies.ProjectsUpdate)]
    public async Task<ActionResult<ApiResponse<Guid>>> CreateTip([FromBody] UpsertTimesheetTipDto dto, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.CreateTipAsync(dto, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<Guid>.Ok(result.Value))
            : BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
    }

    [HttpPut("tips/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.ProjectsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateTip(Guid id, [FromBody] UpsertTimesheetTipDto dto, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.UpdateTipAsync(id, dto, cancellationToken);
        return result.IsSuccess ? Ok(ApiResponse<object>.Ok(null!)) : BadRequest(ApiResponse<object>.Fail(result.Error.Description));
    }

    [HttpDelete("tips/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.ProjectsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTip(Guid id, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.DeleteTipAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(ApiResponse<object>.Ok(null!)) : BadRequest(ApiResponse<object>.Fail(result.Error.Description));
    }

    [HttpPost("timer/start")]
    [Authorize(Policy = PermissionPolicies.ProjectTimeCreate)]
    public async Task<ActionResult<ApiResponse<TimesheetTimerStateDto>>> StartTimer(
        [FromBody] StartTimesheetTimerDto dto, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.StartTimerAsync(dto, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<TimesheetTimerStateDto>.Ok(result.Value))
            : BadRequest(ApiResponse<TimesheetTimerStateDto>.Fail(result.Error.Description));
    }

    [HttpPost("timer/{entryId:guid}/stop")]
    [Authorize(Policy = PermissionPolicies.ProjectTimeCreate)]
    public async Task<ActionResult<ApiResponse<decimal>>> StopTimer(Guid entryId, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.StopTimerAsync(entryId, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<decimal>.Ok(result.Value))
            : BadRequest(ApiResponse<decimal>.Fail(result.Error.Description));
    }

    [HttpGet("timer/active")]
    [Authorize(Policy = PermissionPolicies.ProjectTimeRead)]
    public async Task<ActionResult<ApiResponse<TimesheetTimerStateDto?>>> ActiveTimer(CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        return Ok(ApiResponse<TimesheetTimerStateDto?>.Ok(await _service.GetActiveTimerAsync(cancellationToken)));
    }

    [HttpGet("validation-queue")]
    [Authorize(Policy = PermissionPolicies.ProjectsUpdate)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProjectTimeEntryDto>>>> ValidationQueue(CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        return Ok(ApiResponse<IReadOnlyList<ProjectTimeEntryDto>>.Ok(await _service.ListPendingValidationAsync(cancellationToken)));
    }

    [HttpGet("time-off")]
    [Authorize(Policy = PermissionPolicies.ProjectTimeRead)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TimeOffRequestDto>>>> TimeOff(CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        return Ok(ApiResponse<IReadOnlyList<TimeOffRequestDto>>.Ok(await _service.ListTimeOffRequestsAsync(cancellationToken)));
    }

    [HttpPost("time-off")]
    [Authorize(Policy = PermissionPolicies.ProjectTimeCreate)]
    public async Task<ActionResult<ApiResponse<Guid>>> CreateTimeOff([FromBody] CreateTimeOffRequestDto dto, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.CreateTimeOffRequestAsync(dto, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<Guid>.Ok(result.Value))
            : BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
    }

    [HttpPost("time-off/{id:guid}/approve")]
    [Authorize(Policy = PermissionPolicies.ProjectsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> ApproveTimeOff(Guid id, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.ApproveTimeOffRequestAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(ApiResponse<object>.Ok(null!)) : BadRequest(ApiResponse<object>.Fail(result.Error.Description));
    }

    [HttpPost("time-off/{id:guid}/refuse")]
    [Authorize(Policy = PermissionPolicies.ProjectsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> RefuseTimeOff(Guid id, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.RefuseTimeOffRequestAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(ApiResponse<object>.Ok(null!)) : BadRequest(ApiResponse<object>.Fail(result.Error.Description));
    }
}
