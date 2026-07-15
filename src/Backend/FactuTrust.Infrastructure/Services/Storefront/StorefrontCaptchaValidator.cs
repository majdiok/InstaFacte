using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.Storefront;

/// <summary>
/// V1 stub: when <see cref="StorefrontOptions.OrderSubmissionRequiresCaptcha"/> is false, always succeeds.
/// When true, a Turnstile implementation should be wired (secret in configuration).
/// </summary>
public sealed class StorefrontCaptchaValidator : IStorefrontCaptchaValidator
{
    private readonly StorefrontOptions _options;

    public StorefrontCaptchaValidator(IOptions<StorefrontOptions> options)
    {
        _options = options.Value;
    }

    public Task<bool> IsValidAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (!_options.OrderSubmissionRequiresCaptcha)
            return Task.FromResult(true);

        return Task.FromResult(!string.IsNullOrWhiteSpace(token));
    }
}
