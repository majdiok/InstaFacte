using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Honoraires;

[ApiController]
[Route("api/honoraires/quotes")]
[Authorize]
public sealed class HonorairesQuotesController : ControllerBase
{
    private readonly IHonorairesBillingService _service;

    public HonorairesQuotesController(IHonorairesBillingService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.HonorairesQuotesRead)]
    public async Task<ActionResult<ApiResponse<HonorairesPagedResult<HonorairesQuoteListItemDto>>>> List(
        [FromQuery] HonorairesQuoteStatus? status,
        [FromQuery] Guid? assignmentId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.ListQuotesAsync(status, assignmentId, search, page, pageSize, cancellationToken);
        return Ok(ApiResponse<HonorairesPagedResult<HonorairesQuoteListItemDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.HonorairesQuotesRead)]
    public async Task<ActionResult<ApiResponse<HonorairesQuoteDto>>> Get(Guid id, CancellationToken cancellationToken)
    {
        var dto = await _service.GetQuoteAsync(id, cancellationToken);
        if (dto is null) return NotFound(ApiResponse<HonorairesQuoteDto>.Fail("Devis introuvable"));
        return Ok(ApiResponse<HonorairesQuoteDto>.Ok(dto));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.HonorairesQuotesCreate)]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] UpsertHonorairesQuoteDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.CreateQuoteDraftAsync(dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.HonorairesQuotesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(Guid id, [FromBody] UpsertHonorairesQuoteDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateQuoteDraftAsync(id, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Enregistré"));
    }

    [HttpPost("{id:guid}/send")]
    [Authorize(Policy = PermissionPolicies.HonorairesQuotesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Send(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.SendQuoteAsync(id, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Devis envoyé"));
    }

    [HttpPost("{id:guid}/accept")]
    [Authorize(Policy = PermissionPolicies.HonorairesQuotesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Accept(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.AcceptQuoteAsync(id, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Devis accepté"));
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = PermissionPolicies.HonorairesQuotesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Reject(Guid id, [FromQuery] string? reason, CancellationToken cancellationToken)
    {
        var result = await _service.RejectQuoteAsync(id, reason, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Devis refusé"));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = PermissionPolicies.HonorairesQuotesDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(Guid id, [FromQuery] string? reason, CancellationToken cancellationToken)
    {
        var result = await _service.CancelQuoteAsync(id, reason, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Devis annulé"));
    }

    [HttpPost("{id:guid}/convert")]
    [Authorize(Policy = PermissionPolicies.HonorairesQuotesConvert)]
    public async Task<ActionResult<ApiResponse<Guid>>> Convert(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.ConvertQuoteToInvoiceAsync(id, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value));
    }

    [HttpGet("preview-number")]
    [Authorize(Policy = PermissionPolicies.HonorairesQuotesRead)]
    public async Task<ActionResult<ApiResponse<string>>> PreviewNumber(
        [FromQuery] DateTime? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var number = await _service.PreviewQuoteNumberAsync(referenceDate ?? DateTime.UtcNow, cancellationToken);
        return Ok(ApiResponse<string>.Ok(number));
    }

    [HttpGet("{id:guid}/pdf")]
    [Authorize(Policy = PermissionPolicies.HonorairesQuotesRead)]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.ExportQuotePdfAsync(id, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return File(result.Value, "application/pdf", $"devis-honoraires-{id:N}.pdf");
    }
}
