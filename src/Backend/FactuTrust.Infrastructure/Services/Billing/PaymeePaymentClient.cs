using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Common;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.Billing;

/// <summary>
/// Lot C5 — Client REST pour Paymee.tn (paiement par carte tunisien).
///
/// API : <c>https://app.paymee.tn/api/v1/payments/create</c> (sandbox :
/// <c>https://sandbox.paymee.tn/api/v1/payments/create</c>). Auth via header
/// <c>Authorization: Token &lt;apiToken&gt;</c>.
///
/// Le webhook Paymee envoie un POST avec <c>{ token, payment_status, transaction_id }</c>
/// signé en HMAC SHA-256 (header <c>X-Paymee-Signature</c>).
/// </summary>
public sealed class PaymeePaymentClient : IPaymentProviderClient
{
    public const string ProductionBaseUrl = "https://app.paymee.tn/api/v1";
    public const string SandboxBaseUrl = "https://sandbox.paymee.tn/api/v1";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PaymentProviderConfigService _configService;
    private readonly ILogger<PaymeePaymentClient> _logger;

    public PaymeePaymentClient(
        IHttpClientFactory httpClientFactory,
        PaymentProviderConfigService configService,
        ILogger<PaymeePaymentClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configService = configService;
        _logger = logger;
    }

    public string ProviderCode => PaymentProviderCodes.Paymee;

    public async Task<Result<ProviderInitResult>> InitPaymentAsync(ProviderInitRequest request, CancellationToken cancellationToken = default)
    {
        var configBundle = await _configService.ReadDecryptedAsync(ProviderCode, cancellationToken);
        if (configBundle is null)
            return Result.Failure<ProviderInitResult>(Error.NotFound("PaymentProviderConfig", Guid.Empty));

        var (secretsJson, _, config) = configBundle.Value;
        if (!config.IsEnabled)
            return Result.Failure<ProviderInitResult>(Error.Conflict("Provider Paymee désactivé"));
        if (string.IsNullOrWhiteSpace(secretsJson))
            return Result.Failure<ProviderInitResult>(Error.Conflict("Secrets Paymee non configurés"));

        string? apiToken = null;
        string? vendor = null;
        try
        {
            using var doc = JsonDocument.Parse(secretsJson);
            if (doc.RootElement.TryGetProperty("apiToken", out var t)) apiToken = t.GetString();
            if (doc.RootElement.TryGetProperty("vendor", out var v)) vendor = v.GetString();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Paymee secrets JSON invalide");
            return Result.Failure<ProviderInitResult>(Error.Conflict("Secrets Paymee mal formés"));
        }
        if (string.IsNullOrWhiteSpace(apiToken))
            return Result.Failure<ProviderInitResult>(Error.Conflict("apiToken Paymee manquant"));

        var (firstName, lastName) = SplitFullName(request.CustomerName);

        var body = new Dictionary<string, object?>
        {
            ["amount"] = request.AmountTND,
            ["note"] = request.Description ?? $"Paiement FactuTrust {request.IntentId:N}",
            ["first_name"] = firstName ?? "Client",
            ["last_name"] = lastName ?? "FactuTrust",
            ["email"] = request.CustomerEmail ?? "no-reply@factutrust.tn",
            ["phone"] = "+216",
            ["return_url"] = AppendQuery(request.ReturnUrl, "intent", request.IntentId.ToString("N")),
            ["cancel_url"] = AppendQuery(request.ReturnUrl, "intent", request.IntentId.ToString("N")),
            ["webhook_url"] = (string?)null,
            ["order_id"] = request.IdempotencyKey,
            ["vendor"] = vendor
        };

        var baseUrl = config.IsTestMode ? SandboxBaseUrl : ProductionBaseUrl;
        var http = _httpClientFactory.CreateClient(nameof(PaymeePaymentClient));
        http.DefaultRequestHeaders.Remove("Authorization");
        http.DefaultRequestHeaders.Add("Authorization", $"Token {apiToken}");

        try
        {
            var response = await http.PostAsJsonAsync($"{baseUrl}/payments/create", body, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Paymee init failed: {Status} {Body}", response.StatusCode, raw);
                return Result.Failure<ProviderInitResult>(Error.Conflict($"Paymee a refusé l'initialisation ({(int)response.StatusCode})."));
            }

            var json = JsonNode.Parse(raw);
            var token = json?["data"]?["token"]?.GetValue<string>();
            // L'URL est construite à partir du token côté Paymee.
            var redirectBase = config.IsTestMode ? "https://sandbox.paymee.tn/gateway/" : "https://app.paymee.tn/gateway/";
            if (string.IsNullOrWhiteSpace(token))
                return Result.Failure<ProviderInitResult>(Error.Conflict("Réponse Paymee incomplète"));

            var redirectUrl = redirectBase + token;
            return Result.Success(new ProviderInitResult(token, redirectUrl, raw));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Paymee HTTP error");
            return Result.Failure<ProviderInitResult>(Error.Conflict("Paymee inaccessible"));
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
            // Paymee : { "token": "...", "payment_status": true|false, "transaction_id": "..." }
            var token = root.TryGetProperty("token", out var tk) ? tk.GetString() : null;
            var transactionId = root.TryGetProperty("transaction_id", out var tx) ? tx.GetString() : null;
            bool? success = null;
            if (root.TryGetProperty("payment_status", out var ps))
            {
                success = ps.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => bool.TryParse(ps.GetString(), out var b) ? b : null,
                    _ => null
                };
            }

            if (string.IsNullOrWhiteSpace(token))
                return Result.Failure<ProviderWebhookEvent>(Error.Validation("token", "Manquant"));

            var outcome = success switch
            {
                true => ProviderWebhookOutcome.Succeeded,
                false => ProviderWebhookOutcome.Failed,
                _ => ProviderWebhookOutcome.Unknown
            };
            var eventId = $"{token}:{(success?.ToString().ToLowerInvariant() ?? "pending")}";
            return Result.Success(new ProviderWebhookEvent(eventId, token, outcome, transactionId, payload));
        }
        catch (JsonException ex)
        {
            return Result.Failure<ProviderWebhookEvent>(Error.Validation("Payload", $"JSON invalide : {ex.Message}"));
        }
    }

    private static (string? FirstName, string? LastName) SplitFullName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return (null, null);
        var parts = fullName.Trim().Split(' ', 2);
        return parts.Length == 1 ? (parts[0], null) : (parts[0], parts[1]);
    }

    private static string? AppendQuery(string? url, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var sep = url.Contains('?') ? '&' : '?';
        return $"{url}{sep}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
    }
}
