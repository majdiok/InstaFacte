using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.WithholdingTax.Commands;
using FactuTrust.Application.Features.WithholdingTax.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/withholding-tax")]
[Authorize]
public class WithholdingTaxController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWithholdingTaxService _calculationService;

    public WithholdingTaxController(IMediator mediator, IWithholdingTaxService calculationService)
    {
        _mediator = mediator;
        _calculationService = calculationService;
    }

    [HttpGet("types")]
    [Authorize(Policy = PermissionPolicies.WithholdingTaxRead)]
    public async Task<IActionResult> GetTypes([FromQuery] bool activeOnly = true)
    {
        var result = await _mediator.Send(new GetWithholdingTaxTypesQuery(activeOnly));
        return Ok(result);
    }

    [HttpPost("calculate")]
    [Authorize(Policy = PermissionPolicies.WithholdingTaxRead)]
    public IActionResult Calculate([FromBody] WithholdingCalculationRequest request)
    {
        var result = _calculationService.CalculateWithholding(request);
        return Ok(result);
    }

    [HttpPost("tej-export/generate")]
    [Authorize(Policy = PermissionPolicies.WithholdingTaxExport)]
    public async Task<IActionResult> GenerateTejXml([FromBody] TejXmlExportRequest request)
    {
        var result = await _mediator.Send(new ExportTejXmlCommand(request));
        if (result.IsFailure)
            return BadRequest(new { error = result.Error.Description });

        if (!result.Value.IsValid)
            return Ok(new { errors = result.Value.ValidationErrors, isValid = false });

        return File(result.Value.XmlContent, "application/xml", result.Value.FileName);
    }

    [HttpGet("tej-export/history")]
    [Authorize(Policy = PermissionPolicies.WithholdingTaxRead)]
    public async Task<IActionResult> GetTejExportHistory([FromQuery] int take = 50)
    {
        var items = await _mediator.Send(new GetTejXmlExportLogsQuery(take));
        return Ok(new { items });
    }

    [HttpGet("tej-export/eligible-count")]
    [Authorize(Policy = PermissionPolicies.WithholdingTaxRead)]
    public async Task<IActionResult> GetTejEligibleInvoiceCount([FromQuery] int year, [FromQuery] int month)
    {
        var count = await _mediator.Send(new GetTejEligibleInvoiceCountQuery(year, month));
        return Ok(new { count });
    }

    [HttpPost("tej-export/preview")]
    [Authorize(Policy = PermissionPolicies.WithholdingTaxExport)]
    public async Task<IActionResult> PreviewTejXml([FromBody] TejXmlExportRequest request)
    {
        var result = await _mediator.Send(new ExportTejXmlCommand(request));
        if (result.IsFailure)
            return BadRequest(new { error = result.Error.Description });

        var xmlString = System.Text.Encoding.UTF8.GetString(result.Value.XmlContent);
        return Ok(new
        {
            fileName = result.Value.FileName,
            xmlContent = xmlString,
            validationErrors = result.Value.ValidationErrors,
            certificateCount = result.Value.CertificateCount,
            isValid = result.Value.IsValid
        });
    }

    [HttpGet("dashboard")]
    [Authorize(Policy = PermissionPolicies.WithholdingTaxRead)]
    public async Task<IActionResult> GetDashboard([FromQuery] int? year)
    {
        var result = await _mediator.Send(new GetWithholdingDashboardQuery(year));
        return Ok(result);
    }

    [HttpGet("monthly-report")]
    [Authorize(Policy = PermissionPolicies.WithholdingTaxRead)]
    public async Task<IActionResult> GetMonthlyReport([FromQuery] int year, [FromQuery] int month)
    {
        var result = await _mediator.Send(new GetWithholdingMonthlyReportQuery(year, month));
        return Ok(result);
    }
}
