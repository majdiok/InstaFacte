namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Validates an anti-bot token on public storefront mutations (e.g. Cloudflare Turnstile).
/// </summary>
public interface IStorefrontCaptchaValidator
{
    Task<bool> IsValidAsync(string? token, CancellationToken cancellationToken = default);
}
