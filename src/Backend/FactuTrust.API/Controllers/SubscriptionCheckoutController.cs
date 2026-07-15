using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Lot C5 — Initiation paiement côté tenant + saisie reçu virement côté admin.
///
/// Endpoints :
/// <list type="bullet">
///   <item><c>POST /api/subscription/checkout</c> — tenant authentifié, choix provider, retour URL.</item>
///   <item><c>POST /api/platform/invoices/{id}/wire-receipt</c> — admin saisit manuellement un virement reçu.</item>
/// </list>
/// </summary>
[ApiController]
[Authorize]
public sealed class SubscriptionCheckoutController : ControllerBase
{
    private readonly IPaymentCheckoutService _checkoutService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<SubscriptionCheckoutController> _logger;

    public SubscriptionCheckoutController(
        IPaymentCheckoutService checkoutService,
        ITenantContext tenantContext,
        ILogger<SubscriptionCheckoutController> logger)
    {
        _checkoutService = checkoutService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>Tenant : initie un paiement de facture (Konnect / Paymee / Wire).</summary>
    [HttpPost("api/subscription/checkout")]
    [ProducesResponseType(typeof(ApiResponse<InitiateCheckoutResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InitiateCheckout([FromBody] InitiateCheckoutRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<InitiateCheckoutResponse>.Fail("Requête invalide."));
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<InitiateCheckoutResponse>.Fail("Non authentifié."));

        var tenantId = _tenantContext.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            return BadRequest(ApiResponse<InitiateCheckoutResponse>.Fail("Aucun tenant courant."));

        var result = await _checkoutService.InitiateAsync(request, tenantId, actorId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code switch
            {
                "Forbidden" => StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<InitiateCheckoutResponse>.Fail(result.Error.Description, result.Error.Code)),
                _ when result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal) =>
                    NotFound(ApiResponse<InitiateCheckoutResponse>.Fail(result.Error.Description, result.Error.Code)),
                _ => BadRequest(ApiResponse<InitiateCheckoutResponse>.Fail(result.Error.Description, result.Error.Code))
            };
        }

        _logger.LogInformation("Tenant {TenantId} initiated {Provider} checkout for invoice {InvoiceId}",
            tenantId, request.ProviderCode, request.InvoiceId);
        return Ok(ApiResponse<InitiateCheckoutResponse>.Ok(result.Value, "Checkout initié."));
    }

    /// <summary>
    /// Sous-lot C5.5 — Admin : initie un checkout depuis le backoffice (déduit le tenantId
    /// depuis la facture). Utile pour tester l'intégration provider ou aider un tenant
    /// bloqué côté paiement.
    /// </summary>
    [HttpPost("api/platform/invoices/{invoiceId:guid}/checkout")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceIssue)]
    [ProducesResponseType(typeof(ApiResponse<InitiateCheckoutResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InitiateAdminCheckout(
        Guid invoiceId,
        [FromBody] InitiateCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<InitiateCheckoutResponse>.Fail("Requête invalide."));
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<InitiateCheckoutResponse>.Fail("Non authentifié."));

        // Force l'invoiceId provenant de l'URL (sécurité : on n'autorise pas un mismatch).
        var safeRequest = request with { InvoiceId = invoiceId };

        var result = await _checkoutService.InitiateAdminAsync(safeRequest, actorId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
                ? NotFound(ApiResponse<InitiateCheckoutResponse>.Fail(result.Error.Description, result.Error.Code))
                : BadRequest(ApiResponse<InitiateCheckoutResponse>.Fail(result.Error.Description, result.Error.Code));
        }

        _logger.LogInformation("Platform admin {ActorId} initiated {Provider} admin-checkout for invoice {InvoiceId}",
            actorId, request.ProviderCode, invoiceId);
        return Ok(ApiResponse<InitiateCheckoutResponse>.Ok(result.Value, "Checkout initié."));
    }

    /// <summary>Admin : enregistre un reçu de virement manuel (saisie après réception).</summary>
    [HttpPost("api/platform/invoices/{invoiceId:guid}/wire-receipt")]
    [Authorize(Policy = "perm:" + PlatformPermissions.InvoiceIssue)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterWireReceipt(Guid invoiceId, [FromBody] RegisterWireReceiptRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Requête invalide."));
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId))
            return Unauthorized(ApiResponse<object>.Fail("Non authentifié."));

        var result = await _checkoutService.RegisterWireReceiptAsync(invoiceId, request, actorId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
                ? NotFound(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code))
                : BadRequest(ApiResponse<object>.Fail(result.Error.Description, result.Error.Code));
        }

        _logger.LogInformation("Platform admin {ActorId} registered wire receipt for invoice {InvoiceId}", actorId, invoiceId);
        return Ok(ApiResponse<object>.Ok(null!, "Reçu de virement enregistré."));
    }
}
