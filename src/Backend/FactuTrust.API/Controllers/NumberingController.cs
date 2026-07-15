using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Numbering.Commands;
using FactuTrust.Application.Features.Numbering.Queries;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/settings/numbering")]
[Authorize]
public sealed class NumberingController : ControllerBase
{
    private readonly IMediator _mediator;

    public NumberingController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SettingsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<NumberingSchemeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int? fiscalYear, CancellationToken cancellationToken)
    {
        var schemes = await _mediator.Send(new GetNumberingSchemesQuery(fiscalYear), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<NumberingSchemeDto>>.Ok(schemes));
    }

    [HttpGet("{documentType}")]
    [Authorize(Policy = PermissionPolicies.SettingsRead)]
    [ProducesResponseType(typeof(ApiResponse<NumberingSchemeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByType(
        NumberingDocumentType documentType,
        [FromQuery] int? fiscalYear,
        CancellationToken cancellationToken)
    {
        var scheme = await _mediator.Send(new GetNumberingSchemeByTypeQuery(documentType, fiscalYear), cancellationToken);
        return Ok(ApiResponse<NumberingSchemeDto>.Ok(scheme));
    }

    [HttpPut("{documentType}")]
    [Authorize(Policy = PermissionPolicies.SettingsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<NumberingSchemeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Save(
        NumberingDocumentType documentType,
        [FromBody] SaveNumberingSchemeRequest request,
        [FromQuery] int? fiscalYear,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SaveNumberingSchemeCommand(documentType, request, fiscalYear), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<NumberingSchemeDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<NumberingSchemeDto>.Ok(result.Value));
    }

    [HttpPost("{documentType}/preview")]
    [Authorize(Policy = PermissionPolicies.SettingsRead)]
    [ProducesResponseType(typeof(ApiResponse<PreviewNumberingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(
        NumberingDocumentType documentType,
        [FromBody] PreviewNumberingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _mediator.Send(new PreviewNumberingQuery(documentType, request), cancellationToken);
            return Ok(ApiResponse<PreviewNumberingResponse>.Ok(response));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<PreviewNumberingResponse>.Fail(ex.Message));
        }
    }

    [HttpPost("{documentType}/reset")]
    [Authorize(Policy = PermissionPolicies.SettingsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<NumberingSchemeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reset(
        NumberingDocumentType documentType,
        [FromQuery] int? fiscalYear,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ResetNumberingSchemeCommand(documentType, fiscalYear), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<NumberingSchemeDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<NumberingSchemeDto>.Ok(result.Value));
    }
}
