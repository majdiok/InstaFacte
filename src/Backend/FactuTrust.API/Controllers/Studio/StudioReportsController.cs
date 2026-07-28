using FactuTrust.API.Authorization;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Studio.Common;
using FactuTrust.Application.Features.Studio.Reports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers.Studio;

/// <summary>
/// Custom reports over custom entities. Design (list/get/save/delete/preview) requires
/// <c>studio:design_reports</c>; running a saved report requires <c>custom_reports:view</c>.
/// </summary>
[ApiController]
[Route("api/studio/reports")]
[Authorize]
public sealed class StudioReportsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly OllamaSettings _ollamaSettings;

    public StudioReportsController(IMediator mediator, IOptions<OllamaSettings> ollamaSettings)
    {
        _mediator = mediator;
        _ollamaSettings = ollamaSettings.Value;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.StudioDesignReports)]
    public async Task<IActionResult> List([FromQuery] string? dataSourceRef, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListCustomReportsQuery(dataSourceRef), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<IReadOnlyList<CustomReportDto>>.Ok(result.Value));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.StudioDesignReports)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCustomReportQuery(id), cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<CustomReportDto>.Ok(result.Value));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.StudioDesignReports)]
    public async Task<IActionResult> Create([FromBody] SaveCustomReportRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpsertCustomReportCommand(null, request), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<CustomReportDto>.Ok(result.Value));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.StudioDesignReports)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveCustomReportRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpsertCustomReportCommand(id, request), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<CustomReportDto>.Ok(result.Value));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.StudioDesignReports)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteCustomReportCommand(id), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<object>.Ok(new { }, "Rapport supprimé."));
    }

    /// <summary>Available data sources (custom entities + whitelisted existing sources) + their fields.</summary>
    [HttpGet("sources")]
    [Authorize(Policy = PermissionPolicies.StudioDesignReports)]
    public async Task<IActionResult> Sources(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetReportSourcesQuery(), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<IReadOnlyList<ReportSourceDto>>.Ok(result.Value));
    }

    /// <summary>Live preview for the report designer (ad-hoc definition, not saved).</summary>
    [HttpPost("preview")]
    [Authorize(Policy = PermissionPolicies.StudioDesignReports)]
    public async Task<IActionResult> Preview([FromBody] RunReportPreviewRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RunReportPreviewQuery(request.DataSourceKind, request.DataSourceRef, request.Definition), cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<ReportResultDto>.Ok(result.Value));
    }

    /// <summary>Run a saved report (end-user viewing).</summary>
    [HttpGet("{id:guid}/run")]
    [Authorize(Policy = PermissionPolicies.CustomReportsView)]
    public async Task<IActionResult> Run(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RunSavedReportQuery(id), cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : Ok(ApiResponse<ReportResultDto>.Ok(result.Value));
    }

    /// <summary>
    /// Imprime un état enregistré (PDF). Même exécution que <c>run</c> : ce qui est imprimé est
    /// exactement ce qui est affiché. Coupe-circuit : <c>Ollama:EnableStudioReportPdf</c>.
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    [Authorize(Policy = PermissionPolicies.CustomReportsView)]
    public async Task<IActionResult> ExportPdf(Guid id, CancellationToken cancellationToken)
    {
        if (!_ollamaSettings.EnableStudioReportPdf)
            return NotFound(ApiResponse<object>.Fail("L'impression des états Studio est désactivée.", "Disabled"));

        var result = await _mediator.Send(new ExportStudioReportPdfQuery(id), cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
            : File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }
}
