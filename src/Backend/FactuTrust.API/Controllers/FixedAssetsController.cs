using FactuTrust.API.Authorization;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.FixedAssets;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/accounting/fixed-assets")]
[Authorize]
public sealed class FixedAssetsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly FixedAssetsOptions _options;

    public FixedAssetsController(IMediator mediator, IOptions<FixedAssetsOptions> options)
    {
        _mediator = mediator;
        _options = options.Value;
    }

    private IActionResult? GuardEnabled()
    {
        if (!_options.Enabled)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail(
                "Le sous-module Immobilisations est désactivé. Activez Features:FixedAssets:Enabled."));
        return null;
    }

    [HttpGet("rate-categories")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> GetRateCategories(CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled) return disabled;
        var r = await _mediator.Send(new GetDepreciationRateCategoriesQuery(), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<IReadOnlyList<DepreciationRateCategoryDto>>.Ok(r.Value));
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] FixedAssetStatus? status = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] int? fiscalYear = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } disabled) return disabled;
        var r = await _mediator.Send(new GetFixedAssetsQuery(page, pageSize, status, categoryId, fiscalYear, search), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<FixedAssetListResponse>.Ok(r.Value));
    }

    [HttpGet("amortization-table")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> GetCurrentYearAmortizationTable(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] int? fiscalYear = null,
        [FromQuery] FixedAssetStatus? status = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } disabled) return disabled;
        var r = await _mediator.Send(
            new GetCurrentYearAmortizationTableQuery(page, pageSize, fiscalYear, status, categoryId, search),
            cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<FixedAssetAmortizationTableResponse>.Ok(r.Value));
    }

    [HttpGet("amortization-report")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> GetAmortizationReport(
        [FromQuery] int? fiscalYear = null,
        [FromQuery] AmortizationReportGroupingMode groupingMode = AmortizationReportGroupingMode.AssetAccount,
        [FromQuery] FixedAssetStatus? status = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? companyName = null,
        CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } disabled) return disabled;
        var r = await _mediator.Send(
            new GetAmortizationReportQuery(fiscalYear, groupingMode, status, categoryId, search, companyName),
            cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<AmortizationReportResponse>.Ok(r.Value));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled) return disabled;
        var r = await _mediator.Send(new GetFixedAssetByIdQuery(id), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<FixedAssetDto>.Ok(r.Value));
    }

    [HttpGet("{id:guid}/schedule")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> GetSchedule(Guid id, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled) return disabled;
        var r = await _mediator.Send(new GetFixedAssetScheduleQuery(id), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<FixedAssetScheduleDto>.Ok(r.Value));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    public async Task<IActionResult> Create([FromBody] CreateFixedAssetRequest request, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled) return disabled;
        var r = await _mediator.Send(new CreateFixedAssetCommand(request), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(r.Value));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFixedAssetRequest request, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled) return disabled;
        var r = await _mediator.Send(new UpdateFixedAssetCommand(id, request), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(r.Value));
    }

    [HttpPost("{id:guid}/schedule/preview")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> PreviewSchedule(Guid id, [FromBody] PreviewDepreciationScheduleRequest request, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled) return disabled;
        var r = await _mediator.Send(new PreviewDepreciationScheduleQuery(id, request.InServiceDate), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<FixedAssetScheduleDto>.Ok(r.Value));
    }

    [HttpPost("{id:guid}/put-in-service")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    public async Task<IActionResult> PutInService(Guid id, [FromBody] PutFixedAssetInServiceRequest request, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled) return disabled;
        var r = await _mediator.Send(new PutFixedAssetInServiceCommand(id, request), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(r.Value));
    }

    [HttpPost("{id:guid}/generate-schedule")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    public async Task<IActionResult> GenerateSchedule(Guid id, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled) return disabled;
        var r = await _mediator.Send(new GenerateDepreciationScheduleCommand(id), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<FixedAssetScheduleDto>.Ok(r.Value));
    }

    [HttpPost("depreciation-runs")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    public async Task<IActionResult> PostDepreciationRun([FromBody] PostDepreciationRunRequest request, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled) return disabled;
        var r = await _mediator.Send(new PostDepreciationRunCommand(request), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<DepreciationRunResultDto>.Ok(r.Value));
    }

    [HttpPost("{id:guid}/dispose")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    public async Task<IActionResult> Dispose(Guid id, [FromBody] DisposeFixedAssetRequest request, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled) return disabled;
        var r = await _mediator.Send(new DisposeFixedAssetCommand(id, request), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(r.Value));
    }

    [HttpGet("{id:guid}/schedule/export.xlsx")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> ExportScheduleExcel(Guid id, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled) return disabled;
        var r = await _mediator.Send(new ExportFixedAssetScheduleExcelQuery(id), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return File(r.Value, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"tableau-amortissement-{id}.xlsx");
    }

    [HttpGet("depreciation-report/export.xlsx")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> ExportDepreciationReportExcel([FromQuery] int fiscalYear, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } disabled) return disabled;
        var r = await _mediator.Send(new ExportDepreciationReportExcelQuery(fiscalYear), cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        return File(r.Value, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"dotations-immobilisations-{fiscalYear}.xlsx");
    }

    [HttpGet("amortization-report/export.xlsx")]
    [Authorize(Policy = PermissionPolicies.AccountingRead)]
    public async Task<IActionResult> ExportAmortizationReportExcel(
        [FromQuery] int? fiscalYear = null,
        [FromQuery] AmortizationReportGroupingMode groupingMode = AmortizationReportGroupingMode.AssetAccount,
        [FromQuery] FixedAssetStatus? status = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? companyName = null,
        CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } disabled) return disabled;
        var r = await _mediator.Send(
            new ExportAmortizationReportExcelQuery(fiscalYear, groupingMode, status, categoryId, search, companyName),
            cancellationToken);
        if (r.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(r.Error.Description));
        var year = fiscalYear ?? DateTime.UtcNow.Year;
        return File(r.Value, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"tableau-amortissements-{year}.xlsx");
    }
}