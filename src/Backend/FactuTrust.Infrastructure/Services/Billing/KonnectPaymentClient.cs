using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Common;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.Billing;

/// <summary>
/// Lot C5 — Client REST pour Konnect Network (paiement par carte tunisien).
///
/// API officielle : <c>https://api.konnect.network/api/v2/payments/init-payment</c>
/// (pas de SDK officiel .NET). Auth via header <c>x-api-key</c>.
///
/// Le client est <b>tolérant aux erreurs réseau</b> : tout échec retourne
/// <see cref="Result{T}.Failure"/> au lieu de lever, et la <see cref="PaymentIntent"/>
/// reste à l'état <c>Created</c> côté FactuTrust.
/// </summary>
public sealed class KonnectPaymentClient : IPaymentProviderClient
{
    public const string DefaultBaseUrl = "https://api.konnect.network/api/v2";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PaymentProviderConfigService _configService;
    private readonly ILogger<KonnectPaymentClient> _logger;

    public KonnectPaymentClient(
        IHttpClientFactory httpClientFactory,
        PaymentProviderConfigService configService,
        ILogger<KonnectPaymentClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configService = configService;
        _logger = logger;
    }

    public string ProviderCode => PaymentProviderCodes.Konnect;

    public async Task<Result<ProviderInitResult>> InitPaymentAsync(ProviderInitRequest request, CancellationToken cancellationToken = default)
    {
        var configBundle = await _configService.ReadDecryptedAsync(ProviderCode, cancellationToken);
        if (configBundle is null)
            return Result.Failure<ProviderInitResult>(Error.NotFound("PaymentProviderConfig", Guid.Empty));

        var (secretsJson, _, config) = configBundle.Value;
        if (!config.IsEnabled)
            return Result.Failure<ProviderInitResult>(Error.Conflict("Provider Konnect désactivé"));
        if (string.IsNullOrWhiteSpace(secretsJson))
            return Result.Failure<ProviderInitResult>(Error.Conflict("Secrets Konnect non configurés"));

        string? apiKey = null;
        string? receiverWalletId = null;
        try
        {
            using var doc = JsonDocument.Parse(secretsJson);
            if (doc.RootElement.TryGetProperty("apiKey", out var k)) apiKey = k.GetString();
            if (doc.RootElement.TryGetProperty("receiverWalletId", out var w)) receiverWalletId = w.GetString();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Konnect secrets JSON invalide");
            return Result.Failure<ProviderInitResult>(Error.Conflict("Secrets Konnect mal formés"));
        }
        if (string.IsNullOrWhiteSpace(apiKey))
            return Result.Failure<ProviderInitResult>(Error.Conflict("apiKey Konnect manquant"));

        // Konnect attend les montants en millimes (1 TND = 1000 millimes).
        var amountInMillimes = (int)Math.Round(request.AmountTND * 1000m, 0);

        var body = new Dictionary<string, object?>
        {
            ["receiverWalletId"] = receiverWalletId,
            ["token"] = "TND",
            ["amount"] = amountInMillimes,
            ["type"] = "immediate",
            ["description"] = request.Description ?? $"Paiement {request.IntentId:N}",
            ["acceptedPaymentMethods"] = new[] { "wallet", "bank_card", "e-DINAR" },
            ["lifespan"] = 30,
            ["checkoutForm"] = false,
            ["addPaymentFeesToAmount"] = false,
            ["firstName"] = request.CustomerName,
            ["email"] = request.CustomerEmail,
            ["successUrl"] = AppendQuery(request.ReturnUrl, "intent", request.IntentId.ToString("N")),
            ["failUrl"] = AppendQuery(request.ReturnUrl, "intent", request.IntentId.ToString("N")),
            ["theme"] = "light",
            ["webhook"] = (string?)null,
            ["orderId"] = request.IdempotencyKey
        };

        var http = _httpClientFactory.CreateClient(nameof(KonnectPaymentClient));
        http.DefaultRequestHeaders.Remove("x-api-key");
        http.DefaultRequestHeaders.Add("x-api-key", apiKey);

        try
        {
            var response = await http.PostAsJsonAsync(
                $"{DefaultBaseUrl}/payments/init-payment",
                body,
                cancellationToken: cancellationToken);

            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Konnect init failed: {Status} {Body}", response.StatusCode, raw);
                return Result.Failure<ProviderInitResult>(Error.Conflict($"Konnect a refusé l'initialisation ({(int)response.StatusCode})."));
            }

            var json = JsonNode.Parse(raw);
            var paymentRef = json?["paymentRef"]?.GetValue<string>();
            var redirectUrl = json?["payUrl"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(paymentRef) || string.IsNullOrWhiteSpace(redirectUrl))
                return Result.Failure<ProviderInitResult>(Error.Conflict("Réponse Konnect incomplète"));

            return Result.Success(new ProviderInitResult(paymentRef, redirectUrl, raw));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Konnect HTTP error");
            return Result.Failure<ProviderInitResult>(Error.Conflict("Konnect inaccessible"));
        }
    }

    public Result<ProviderWebhookEvent> ParseWebhook(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return Result.Failure<ProviderWebhookEvent>(Error.Validation("Payload", "Webhook vide"));

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            // Konnect envoie { "paymentRef": "...", "status": "completed|pending|failed|cancelled" }
            var paymentRef = root.TryGetProperty("paymentRef", out var pr) ? pr.GetString() : null;
            var status = (root.TryGetProperty("status", out var st) ? st.GetString() : null)?.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(paymentRef))
                return Result.Failure<ProviderWebhookEvent>(Error.Validation("paymentRef", "Manquant"));

            var outcome = status switch
            {
                "completed" or "succeeded" or "success" => ProviderWebhookOutcome.Succeeded,
                "pending" => ProviderWebhookOutcome.Pending,
                "failed" or "error" => ProviderWebhookOutcome.Failed,
                "cancelled" or "canceled" => ProviderWebhookOutcome.Cancelled,
                "refunded" => ProviderWebhookOutcome.Refunded,
                _ => ProviderWebhookOutcome.Unknown
            };
            var failureReason = root.TryGetProperty("reason", out var rsn) ? rsn.GetString() : null;
            // EventId : on combine paymentRef + status (Konnect ne fournit pas d'eventId distinct)
            var eventId = $"{paymentRef}:{status ?? "unknown"}";
            return Result.Success(new ProviderWebhookEvent(eventId, paymentRef, outcome, failureReason, payload));
        }
        catch (JsonException ex)
        {
            return Result.Failure<ProviderWebhookEvent>(Error.Validation("Payload", $"JSON invalide : {ex.Message}"));
        }
    }

    private static string? AppendQuery(string? url, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var sep = url.Contains('?') ? '&' : '?';
        return $"{url}{sep}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
    }
}
