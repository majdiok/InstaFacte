using System.Text;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Lot C5 — Endpoints publics recevant les webhooks providers.
///
/// <list type="bullet">
///   <item><c>POST /api/webhooks/konnect</c> — header <c>X-Konnect-Signature</c></item>
///   <item><c>POST /api/webhooks/paymee</c> — header <c>X-Paymee-Signature</c></item>
/// </list>
///
/// Auth : <see cref="AllowAnonymousAttribute"/> mais validation HMAC dans le handler.
/// Le payload est lu en raw (string) pour conserver les bytes signés exactement.
/// Toujours répondre 200 (sauf erreur 5xx réelle) pour éviter le retry agressif des providers.
/// </summary>
[ApiController]
[Route("api/webhooks")]
[AllowAnonymous]
public sealed class WebhooksController : ControllerBase
{
    private readonly IPaymentWebhookHandler _handler;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(IPaymentWebhookHandler handler, ILogger<WebhooksController> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [HttpPost("konnect")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Konnect(CancellationToken cancellationToken)
        => await ProcessAsync(PaymentProviderCodes.Konnect, "X-Konnect-Signature", cancellationToken);

    [HttpPost("paymee")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Paymee(CancellationToken cancellationToken)
        => await ProcessAsync(PaymentProviderCodes.Paymee, "X-Paymee-Signature", cancellationToken);

    private async Task<IActionResult> ProcessAsync(string providerCode, string signatureHeaderName, CancellationToken cancellationToken)
    {
        Request.EnableBuffering();
        Request.Body.Position = 0;
        string payload;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            payload = await reader.ReadToEndAsync(cancellationToken);
        }

        var signatureHeader = Request.Headers.TryGetValue(signatureHeaderName, out var sig) ? sig.ToString() : null;

        var result = await _handler.HandleAsync(providerCode, payload, signatureHeader, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Webhook {Provider} processing failed: {Code} {Description}", providerCode, result.Error.Code, result.Error.Description);
            // 401 si signature invalide, sinon 400. Les providers re-essaient sur 5xx donc on évite.
            if (result.Error.Code == "Unauthorized")
                return Unauthorized(new { error = result.Error.Description });
            return BadRequest(new { error = result.Error.Description });
        }

        return Ok(new { received = true });
    }
}
