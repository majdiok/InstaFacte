using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Storefront;

/// <summary>
/// Server-side Turnstile verification. Siteverify owns token expiry and single-use enforcement.
/// </summary>
public sealed class StorefrontCaptchaValidator : IStorefrontCaptchaValidator
{
    private const string SiteverifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
    private const int MaxTokenLength = 2048;
    private const int MaxResponseBytes = 16 * 1024;
    private static readonly UTF8Encoding ResponseEncoding = new(false, true);
    private readonly HttpClient _httpClient;
    private readonly StorefrontOptions _options;

    public StorefrontCaptchaValidator(HttpClient httpClient, IOptions<StorefrontOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<bool> IsValidAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (!_options.OrderSubmissionRequiresCaptcha)
            return true;

        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength
            || string.IsNullOrWhiteSpace(_options.TurnstileSecretKey)
            || _options.TurnstileAllowedHostnames is not { Length: > 0 } hostnames
            || hostnames.Any(hostname => !IsDnsHostname(hostname))
            || !IsValidExpectedAction(_options.TurnstileExpectedAction))
            return false;

        // HttpClient's timeout stops at response headers with ResponseHeadersRead.
        // This linked deadline also bounds reading the body, without swallowing caller cancellation.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, SiteverifyUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["secret"] = _options.TurnstileSecretKey,
                    ["response"] = token
                })
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxResponseBytes)
                return false;

            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            var buffer = new byte[MaxResponseBytes + 1];
            var length = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(length), deadline.Token)) > 0)
            {
                length += read;
                if (length > MaxResponseBytes)
                    return false;
            }

            deadline.Token.ThrowIfCancellationRequested();
            ResponseEncoding.GetCharCount(buffer, 0, length); // Reject invalid UTF-8, rather than replacing it.
            using var document = JsonDocument.Parse(buffer.AsMemory(0, length), new JsonDocumentOptions { MaxDepth = 16 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !TryGetSingleProperty(root, "success", out var success) || success.ValueKind != JsonValueKind.True
                || !TryGetSingleProperty(root, "hostname", out var hostname) || hostname.ValueKind != JsonValueKind.String
                || !IsDnsHostname(hostname.GetString())
                || !hostnames.Contains(hostname.GetString(), StringComparer.OrdinalIgnoreCase))
                return false;

            return string.IsNullOrEmpty(_options.TurnstileExpectedAction)
                || (TryGetSingleProperty(root, "action", out var action) && action.ValueKind == JsonValueKind.String
                    && string.Equals(action.GetString(), _options.TurnstileExpectedAction, StringComparison.Ordinal));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
        catch (IOException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static bool TryGetSingleProperty(JsonElement root, string name, out JsonElement value)
    {
        value = default;
        var count = 0;
        foreach (var property in root.EnumerateObject())
        {
            if (property.NameEquals(name))
            {
                value = property.Value;
                count++;
            }
        }

        return count == 1;
    }

    private static bool IsDnsHostname(string? hostname)
    {
        if (string.IsNullOrEmpty(hostname) || hostname.Length > 253
            || Uri.CheckHostName(hostname) != UriHostNameType.Dns
            || IPAddress.TryParse(hostname, out _))
            return false;

        return hostname.Split('.').All(label => label.Length is > 0 and <= 63
            && char.IsAsciiLetterOrDigit(label[0]) && char.IsAsciiLetterOrDigit(label[^1])
            && label.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'));
    }

    private static bool IsValidExpectedAction(string? action)
    {
        return string.IsNullOrEmpty(action) || (action.Length <= 32
            && action.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-'));
    }
}
