using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Audit;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/accounting/audit")]
[Authorize]
public sealed class AccountingAuditController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAccountingAuditExportService _export;
    private readonly IAccountingAuditScheduleService _schedules;
    private readonly IAccountingAuditRuleSettingsService _ruleSettings;

    public AccountingAuditController(
        IMediator mediator,
        IAccountingAuditExportService export,
        IAccountingAuditScheduleService schedules,
        IAccountingAuditRuleSettingsService ruleSettings)
    {
        _mediator = mediator;
        _export = export;
        _schedules = schedules;
        _ruleSettings = ruleSettings;
    }

    [HttpPost("runs")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> RunAudit([FromBody] AccountingAuditRunRequestDto request, CancellationToken ct)
    {
        var r = await _mediator.Send(new RunAccountingAuditCommand(request), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<AccountingAuditRunResultDto>.Ok(r.Value));
    }

    [HttpGet("runs/{runId:guid}")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> GetRunStatus(Guid runId, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetAccountingAuditRunStatusQuery(runId), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<AccountingAuditRunStatusDto>.Ok(r.Value));
    }

    [HttpGet("dashboard")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> GetDashboard([FromQuery] int fiscalYear, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetAccountingAuditDashboardQuery(fiscalYear), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<AccountingAuditDashboardDto>.Ok(r.Value));
    }

    [HttpGet("anomalies")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> GetAnomalies([FromQuery] AccountingAnomalyFilterDto filter, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetAccountingAuditAnomaliesQuery(filter), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<PagedAnomaliesDto>.Ok(r.Value));
    }

    [HttpGet("anomalies/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> GetAnomalyDetail(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetAccountingAuditAnomalyDetailQuery(id), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<AccountingAnomalyDetailDto>.Ok(r.Value));
    }

    [HttpPatch("anomalies/{id:guid}/assign")]
    [Authorize(Policy = PermissionPolicies.AccountingValidate)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignAnomalyRequestDto request, CancellationToken ct)
    {
        var r = await _mediator.Send(new AssignAccountingAnomalyCommand(id, request), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<AccountingAnomalyDetailDto>.Ok(r.Value));
    }

    [HttpPatch("anomalies/{id:guid}/status")]
    [Authorize(Policy = PermissionPolicies.AccountingValidate)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateAnomalyStatusRequestDto request, CancellationToken ct)
    {
        var r = await _mediator.Send(new UpdateAccountingAnomalyStatusCommand(id, request), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<AccountingAnomalyDetailDto>.Ok(r.Value));
    }

    [HttpPost("anomalies/{id:guid}/comments")]
    [Authorize(Policy = PermissionPolicies.AccountingValidate)]
    public async Task<IActionResult> Comment(Guid id, [FromBody] CommentAnomalyRequestDto request, CancellationToken ct)
    {
        var r = await _mediator.Send(new CommentAccountingAnomalyCommand(id, request), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<AccountingAnomalyDetailDto>.Ok(r.Value));
    }

    [HttpPost("anomalies/{id:guid}/ignore")]
    [Authorize(Policy = PermissionPolicies.AccountingValidate)]
    public async Task<IActionResult> Ignore(Guid id, [FromBody] IgnoreAnomalyRequestDto request, CancellationToken ct)
    {
        var r = await _mediator.Send(new IgnoreAccountingAnomalyCommand(id, request), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<AccountingAnomalyDetailDto>.Ok(r.Value));
    }

    [HttpPost("anomalies/{id:guid}/resolve")]
    [Authorize(Policy = PermissionPolicies.AccountingValidate)]
    public async Task<IActionResult> Resolve(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new ResolveAccountingAnomalyCommand(id), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<AccountingAnomalyDetailDto>.Ok(r.Value));
    }

    [HttpGet("analytics")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> GetAnalytics([FromQuery] int fiscalYear, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetAccountingAuditAnalyticsQuery(fiscalYear), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<AccountingAuditAnalyticsDto>.Ok(r.Value));
    }

    [HttpGet("modules")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> GetModules([FromQuery] int fiscalYear, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetAccountingAuditModulesQuery(fiscalYear), ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<IReadOnlyList<AccountingControlModuleDto>>.Ok(r.Value));
    }

    [HttpGet("export")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> Export([FromQuery] AccountingAnomalyFilterDto filter, [FromQuery] string format = "csv", CancellationToken ct = default)
    {
        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            var pdf = await _export.ExportPdfAsync(filter.FiscalYear, ct);
            if (pdf.IsFailure) return BadRequest(ApiResponse<object>.Fail(pdf.Error.Description));
            return File(pdf.Value, "application/pdf", $"audit-{filter.FiscalYear}.pdf");
        }
        var csv = await _export.ExportCsvAsync(filter, ct);
        if (csv.IsFailure) return BadRequest(ApiResponse<object>.Fail(csv.Error.Description));
        return File(csv.Value, "text/csv", $"anomalies-{filter.FiscalYear}.csv");
    }

    [HttpGet("schedules")]
    [Authorize(Policy = PermissionPolicies.AccountingClose)]
    public async Task<IActionResult> ListSchedules(CancellationToken ct)
    {
        var r = await _schedules.ListSchedulesAsync(ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<IReadOnlyList<AccountingControlScheduleDto>>.Ok(r.Value));
    }

    [HttpPost("schedules")]
    [Authorize(Policy = PermissionPolicies.AccountingClose)]
    public async Task<IActionResult> SaveSchedule([FromBody] AccountingControlScheduleDto dto, CancellationToken ct)
    {
        var r = await _schedules.SaveScheduleAsync(dto, ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<AccountingControlScheduleDto>.Ok(r.Value));
    }

    [HttpDelete("schedules/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.AccountingClose)]
    public async Task<IActionResult> DeleteSchedule(Guid id, CancellationToken ct)
    {
        var r = await _schedules.DeleteScheduleAsync(id, ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<bool>.Ok(r.Value));
    }

    [HttpGet("rules")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> GetRules(CancellationToken ct)
    {
        var r = await _ruleSettings.GetSettingsAsync(ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<IReadOnlyList<AccountingControlRuleSettingDto>>.Ok(r.Value));
    }

    [HttpPut("rules")]
    [Authorize(Policy = PermissionPolicies.AccountingClose)]
    public async Task<IActionResult> SaveRules([FromBody] IReadOnlyList<AccountingControlRuleSettingDto> settings, CancellationToken ct)
    {
        var r = await _ruleSettings.SaveSettingsAsync(settings, ct);
        if (r.IsFailure) return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<IReadOnlyList<AccountingControlRuleSettingDto>>.Ok(r.Value));
    }
}
