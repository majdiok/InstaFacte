using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Lot C4 (complément) — Endpoints lecture seule des factures plateforme côté tenant.
///
/// Le tenant authentifié récupère <b>uniquement ses propres factures</b> (filtre forcé par
/// <see cref="ITenantContext.TenantId"/>). Aucune modification possible — l'émission, l'annulation
/// et la saisie de reçus restent côté admin plateforme.
///
/// Routes :
/// <list type="bullet">
///   <item><c>GET /api/subscription/invoices</c> — liste paginée</item>
///   <item><c>GET /api/subscription/invoices/{id}</c> — détail (forbidden si autre tenant)</item>
///   <item><c>GET /api/subscription/invoices/{id}/pdf</c> — PDF (forbidden si autre tenant)</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/subscription/invoices")]
[Authorize]
public sealed class SubscriptionInvoicesController : ControllerBase
{
    private readonly IPlatformInvoiceAdminService _service;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<SubscriptionInvoicesController> _logger;

    public SubscriptionInvoicesController(
        IPlatformInvoiceAdminService service,
        ITenantContext tenantContext,
        ILogger<SubscriptionInvoicesController> logger)
    {
        _service = service;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PlatformInvoicesPageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            return BadRequest(ApiResponse<PlatformInvoicesPageDto>.Fail("Aucun tenant courant."));

        var dto = await _service.ListForTenantAsync(tenantId, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PlatformInvoicesPageDto>.Ok(dto));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PlatformInvoiceDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            return BadRequest(ApiResponse<PlatformInvoiceDetailDto>.Fail("Aucun tenant courant."));

        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
            return NotFound(ApiResponse<PlatformInvoiceDetailDto>.Fail(result.Error.Description, result.Error.Code));
        if (result.Value.TenantId != tenantId)
        {
            _logger.LogWarning("Tenant {TenantId} tried to access invoice {InvoiceId} owned by another tenant.", tenantId, id);
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<PlatformInvoiceDetailDto>.Fail("Cette facture ne vous appartient pas.", "Forbidden"));
        }
        return Ok(ApiResponse<PlatformInvoiceDetailDto>.Ok(result.Value));
    }

    [HttpGet("{id:guid}/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            return BadRequest(ApiResponse<object>.Fail("Aucun tenant courant."));

        // Vérifie l'appartenance avant de servir le PDF.
        var detailResult = await _service.GetByIdAsync(id, cancellationToken);
        if (detailResult.IsFailure)
            return NotFound(ApiResponse<object>.Fail(detailResult.Error.Description, detailResult.Error.Code));
        if (detailResult.Value.TenantId != tenantId)
        {
            _logger.LogWarning("Tenant {TenantId} tried to download PDF of invoice {InvoiceId} owned by another tenant.", tenantId, id);
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail("Cette facture ne vous appartient pas.", "Forbidden"));
        }

        var pdfResult = await _service.GetPdfAsync(id, cancellationToken);
        if (pdfResult.IsFailure)
            return NotFound(ApiResponse<object>.Fail(pdfResult.Error.Description, pdfResult.Error.Code));
        return File(pdfResult.Value, "application/pdf", $"facture-{id:N}.pdf");
    }
}
