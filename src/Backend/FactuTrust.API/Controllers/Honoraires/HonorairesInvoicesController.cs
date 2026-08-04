using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Honoraires;

[ApiController]
[Route("api/honoraires/invoices")]
[Authorize]
public sealed class HonorairesInvoicesController : ControllerBase
{
    private readonly IHonorairesBillingService _service;

    public HonorairesInvoicesController(IHonorairesBillingService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.HonorairesInvoicesRead)]
    public async Task<ActionResult<ApiResponse<HonorairesPagedResult<HonorairesInvoiceListItemDto>>>> List(
        [FromQuery] HonorairesDocumentType? type,
        [FromQuery] HonorairesInvoiceStatus? status,
        [FromQuery] Guid? assignmentId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.ListInvoicesAsync(type, status, assignmentId, search, page, pageSize, cancellationToken);
        return Ok(ApiResponse<HonorairesPagedResult<HonorairesInvoiceListItemDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.HonorairesInvoicesRead)]
    public async Task<ActionResult<ApiResponse<HonorairesInvoiceDto>>> Get(Guid id, CancellationToken cancellationToken)
    {
        var dto = await _service.GetInvoiceAsync(id, cancellationToken);
        if (dto is null) return NotFound(ApiResponse<HonorairesInvoiceDto>.Fail("Facture introuvable"));
        return Ok(ApiResponse<HonorairesInvoiceDto>.Ok(dto));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.HonorairesInvoicesCreate)]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] UpsertHonorairesInvoiceDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.CreateInvoiceDraftAsync(dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.HonorairesInvoicesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(Guid id, [FromBody] UpsertHonorairesInvoiceDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateInvoiceDraftAsync(id, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Enregistré"));
    }

    [HttpPost("{id:guid}/validate")]
    [Authorize(Policy = PermissionPolicies.HonorairesInvoicesValidate)]
    public async Task<ActionResult<ApiResponse<object>>> Validate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.ValidateInvoiceAsync(id, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Facture finalisée"));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = PermissionPolicies.HonorairesInvoicesDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(Guid id, [FromQuery] string? reason, CancellationToken cancellationToken)
    {
        var result = await _service.CancelInvoiceAsync(id, reason, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Annulée"));
    }

    [HttpPost("credit-notes")]
    [Authorize(Policy = PermissionPolicies.HonorairesInvoicesCreate)]
    public async Task<ActionResult<ApiResponse<Guid>>> CreateCreditNote([FromBody] CreateHonorairesCreditNoteDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.CreateCreditNoteAsync(dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value));
    }

    [HttpGet("preview-number")]
    [Authorize(Policy = PermissionPolicies.HonorairesInvoicesRead)]
    public async Task<ActionResult<ApiResponse<string>>> PreviewNumber(
        [FromQuery] HonorairesDocumentType type = HonorairesDocumentType.Invoice,
        [FromQuery] DateTime? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var number = await _service.PreviewInvoiceNumberAsync(type, referenceDate ?? DateTime.UtcNow, cancellationToken);
        return Ok(ApiResponse<string>.Ok(number));
    }

    [HttpGet("{id:guid}/pdf")]
    [Authorize(Policy = PermissionPolicies.HonorairesInvoicesRead)]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.ExportInvoicePdfAsync(id, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return File(result.Value, "application/pdf", $"honoraires-{id:N}.pdf");
    }

    [HttpPost("{id:guid}/payments")]
    [Authorize(Policy = PermissionPolicies.HonorairesPaymentsCreate)]
    public async Task<ActionResult<ApiResponse<Guid>>> RecordPayment(Guid id, [FromBody] RecordHonorairesPaymentDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.RecordPaymentAsync(id, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value));
    }

    [HttpGet("{id:guid}/payments")]
    [Authorize(Policy = PermissionPolicies.HonorairesPaymentsRead)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HonorairesPaymentDto>>>> ListPayments(Guid id, CancellationToken cancellationToken)
    {
        var items = await _service.ListPaymentsAsync(id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<HonorairesPaymentDto>>.Ok(items));
    }
}
