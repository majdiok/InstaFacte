using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Lot C4 — CRUD factures plateforme + cycle (issue / cancel) + PDF + reçus.
/// </summary>
[ApiController]
[Route("api/platform/invoices")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformInvoicesController : ControllerBase
{
    private readonly IPlatformInvoiceAdminService _service;
    private readonly IPlatformReceiptAdminService _receipts;
    private readonly ILogger<PlatformInvoicesController> _logger;

    public PlatformInvoicesController(
        IPlatformInvoiceAdminService service,
        IPlatformReceiptAdminService receipts,
        ILogger<PlatformInvoicesController> logger)
    {
        _service = service;
        _receipts = receipts;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceRead)]
    [ProducesResponseType(typeof(ApiResponse<PlatformInvoicesPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var dto = await _service.ListAsync(tenantId, status, from, to, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PlatformInvoicesPageDto>.Ok(dto));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceRead)]
    [ProducesResponseType(typeof(ApiResponse<PlatformInvoiceDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<PlatformInvoiceDetailDto>.Fail(result.Error.Description, result.Error.Code));
        return Ok(ApiResponse<PlatformInvoiceDetailDto>.Ok(result.Value));
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceIssue)]
    [ProducesResponseType(typeof(ApiResponse<PlatformInvoiceDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePlatformInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PlatformInvoiceDetailDto>.Fail("Requête invalide."));
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<PlatformInvoiceDetailDto>.Fail("Non authentifié."));

        var result = await _service.CreateDraftAsync(request, actorId, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<PlatformInvoiceDetailDto>.Fail(result.Error.Description, result.Error.Code));

        _logger.LogInformation("Platform admin {ActorId} created invoice draft {InvoiceId} for tenant {TenantId}", actorId, result.Value.Id, request.TenantId);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id },
            ApiResponse<PlatformInvoiceDetailDto>.Ok(result.Value, "Brouillon créé."));
    }

    [HttpPost("{id:guid}/issue")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceIssue)]
    [ProducesResponseType(typeof(ApiResponse<PlatformInvoiceDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Issue(Guid id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<PlatformInvoiceDetailDto>.Fail("Non authentifié."));

        var result = await _service.IssueAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
                ? NotFound(ApiResponse<PlatformInvoiceDetailDto>.Fail(result.Error.Description, result.Error.Code))
                : BadRequest(ApiResponse<PlatformInvoiceDetailDto>.Fail(result.Error.Description, result.Error.Code));
        }

        _logger.LogInformation("Platform admin {ActorId} issued invoice {Number}", actorId, result.Value.Number);
        return Ok(ApiResponse<PlatformInvoiceDetailDto>.Ok(result.Value, "Facture émise."));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceCancel)]
    [ProducesResponseType(typeof(ApiResponse<PlatformInvoiceDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelPlatformInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PlatformInvoiceDetailDto>.Fail("Requête invalide."));
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<PlatformInvoiceDetailDto>.Fail("Non authentifié."));

        var result = await _service.CancelAsync(id, request, actorId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
                ? NotFound(ApiResponse<PlatformInvoiceDetailDto>.Fail(result.Error.Description, result.Error.Code))
                : BadRequest(ApiResponse<PlatformInvoiceDetailDto>.Fail(result.Error.Description, result.Error.Code));
        }

        _logger.LogInformation("Platform admin {ActorId} cancelled invoice {Number}", actorId, result.Value.Number);
        return Ok(ApiResponse<PlatformInvoiceDetailDto>.Ok(result.Value, "Facture annulée."));
    }

    [HttpGet("{id:guid}/pdf")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetPdfAsync(id, cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));
        return File(result.Value, "application/pdf", $"facture-{id:N}.pdf");
    }

    /// <summary>Lot C4 (complément) — Émet un avoir partiel (sans annuler la facture d'origine).</summary>
    [HttpPost("{id:guid}/credit-note")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceCancel)]
    [ProducesResponseType(typeof(ApiResponse<PlatformInvoiceDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> IssueCreditNote(Guid id, [FromBody] IssueCreditNoteRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PlatformInvoiceDetailDto>.Fail("Requête invalide."));
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<PlatformInvoiceDetailDto>.Fail("Non authentifié."));

        var result = await _service.IssueCreditNoteAsync(id, request, actorId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
                ? NotFound(ApiResponse<PlatformInvoiceDetailDto>.Fail(result.Error.Description, result.Error.Code))
                : BadRequest(ApiResponse<PlatformInvoiceDetailDto>.Fail(result.Error.Description, result.Error.Code));
        }

        _logger.LogInformation("Platform admin {ActorId} issued credit note {Number} (source invoice {SourceId})",
            actorId, result.Value.Number, id);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id },
            ApiResponse<PlatformInvoiceDetailDto>.Ok(result.Value, "Avoir émis."));
    }

    /// <summary>Lot C4 (complément) — PDF d'un reçu (encaissement) plateforme.</summary>
    [HttpGet("receipts/{receiptId:guid}/pdf")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReceiptPdf(Guid receiptId, CancellationToken cancellationToken)
    {
        var result = await _receipts.GetPdfAsync(receiptId, cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));
        return File(result.Value, "application/pdf", $"recu-{receiptId:N}.pdf");
    }

    /// <summary>Lot C4 (complément) — Agrégation TVA par mois pour conformité DGI plateforme.</summary>
    [HttpGet("/api/platform/fiscal/vat-periods")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PlatformVatPeriodDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVatPeriods([FromQuery] int year, CancellationToken cancellationToken)
    {
        if (year <= 0) year = DateTime.UtcNow.Year;
        var list = await _service.GetVatPeriodsAsync(year, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PlatformVatPeriodDto>>.Ok(list));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Receipts
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/receipts")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceIssue)]
    [ProducesResponseType(typeof(ApiResponse<PlatformReceiptDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddReceipt(Guid id, [FromBody] CreatePlatformReceiptRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PlatformReceiptDto>.Fail("Requête invalide."));
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<PlatformReceiptDto>.Fail("Non authentifié."));

        var result = await _receipts.CreateAsync(id, request, actorId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
                ? NotFound(ApiResponse<PlatformReceiptDto>.Fail(result.Error.Description, result.Error.Code))
                : BadRequest(ApiResponse<PlatformReceiptDto>.Fail(result.Error.Description, result.Error.Code));
        }

        _logger.LogInformation("Platform admin {ActorId} added receipt {Number} of {Amount} TND on invoice {InvoiceId}",
            actorId, result.Value.ReceiptNumber, result.Value.AmountTND, id);
        return CreatedAtAction(nameof(GetById), new { id }, ApiResponse<PlatformReceiptDto>.Ok(result.Value, "Reçu enregistré."));
    }

    [HttpPost("receipts/{receiptId:guid}/cancel")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceCancel)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelReceipt(Guid receiptId, [FromBody] CancelPlatformReceiptRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Requête invalide."));
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("Non authentifié."));

        var result = await _receipts.CancelAsync(receiptId, request, actorId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
                ? NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
                : BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));
        }
        return Ok(ApiResponse<object>.Ok(null!, "Reçu annulé."));
    }

    [HttpPost("receipts/{receiptId:guid}/confirm")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceIssue)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmReceipt(Guid receiptId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("Non authentifié."));

        var result = await _receipts.ConfirmAsync(receiptId, actorId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
                ? NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
                : BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));
        }
        return Ok(ApiResponse<object>.Ok(null!, "Reçu confirmé."));
    }
}
