using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.DocumentTemplates.Commands;
using FactuTrust.Application.Features.DocumentTemplates.Queries;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Configuration des modèles visuels d'impression par type de document (calqué sur la numérotation).
/// </summary>
[ApiController]
[Route("api/settings/document-templates")]
[Authorize]
public sealed class DocumentTemplatesController : ControllerBase
{
    private readonly IMediator _mediator;

    public DocumentTemplatesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SettingsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DocumentTemplatePreferenceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var prefs = await _mediator.Send(new GetDocumentTemplatesQuery(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DocumentTemplatePreferenceDto>>.Ok(prefs));
    }

    [HttpGet("catalog")]
    [Authorize(Policy = PermissionPolicies.SettingsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DocumentTemplateCatalogItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCatalog([FromQuery] PrintableDocumentType? documentType, CancellationToken cancellationToken)
    {
        var catalog = await _mediator.Send(new GetDocumentTemplateCatalogQuery(documentType), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DocumentTemplateCatalogItemDto>>.Ok(catalog));
    }

    [HttpGet("{documentType}")]
    [Authorize(Policy = PermissionPolicies.SettingsRead)]
    [ProducesResponseType(typeof(ApiResponse<DocumentTemplatePreferenceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByType(PrintableDocumentType documentType, CancellationToken cancellationToken)
    {
        var pref = await _mediator.Send(new GetDocumentTemplateByTypeQuery(documentType), cancellationToken);
        return Ok(ApiResponse<DocumentTemplatePreferenceDto>.Ok(pref));
    }

    [HttpPut("{documentType}")]
    [Authorize(Policy = PermissionPolicies.SettingsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<DocumentTemplatePreferenceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Save(
        PrintableDocumentType documentType,
        [FromBody] SaveDocumentTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SaveDocumentTemplateCommand(documentType, request), cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<DocumentTemplatePreferenceDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<DocumentTemplatePreferenceDto>.Ok(result.Value));
    }

    [HttpGet("{documentType}/preview")]
    [Authorize(Policy = PermissionPolicies.SettingsRead)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(
        PrintableDocumentType documentType,
        [FromQuery] string? templateKey,
        CancellationToken cancellationToken)
    {
        var pdf = await _mediator.Send(new PreviewDocumentTemplateQuery(documentType, templateKey), cancellationToken);
        return File(pdf, "application/pdf", $"apercu-{documentType}.pdf");
    }
}
