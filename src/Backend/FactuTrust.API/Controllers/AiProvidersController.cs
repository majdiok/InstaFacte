using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers;

[Route("api/ai/providers")]
[ApiController]
[Authorize(Roles = nameof(UserRole.Administrator))]
public sealed class AiProvidersController : ControllerBase
{
    private readonly ITenantAiProviderRepository _repository;
    private readonly IDataProtector _protector;
    private readonly OpenRouterSettings _defaults;

    public AiProvidersController(
        ITenantAiProviderRepository repository,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<OpenRouterSettings> defaults)
    {
        _repository = repository;
        _protector = dataProtectionProvider.CreateProtector("TenantAiProviderSecrets");
        _defaults = defaults.Value;
    }

    /// <summary>Get OpenRouter configuration (masked key).</summary>
    [HttpGet("openrouter")]
    [ProducesResponseType(typeof(ApiResponse<OpenRouterProviderSettingsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOpenRouter(CancellationToken cancellationToken)
    {
        var row = await _repository.GetByProviderKeyAsync("openrouter", cancellationToken);
        var response = MapOpenRouter(row);
        return Ok(ApiResponse<OpenRouterProviderSettingsResponse>.Ok(response));
    }

    /// <summary>Update OpenRouter configuration.</summary>
    [HttpPut("openrouter")]
    [ProducesResponseType(typeof(ApiResponse<OpenRouterProviderSettingsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PutOpenRouter(
        [FromBody] UpdateOpenRouterProviderRequest body,
        CancellationToken cancellationToken)
    {
        if (body.IsEnabled && string.IsNullOrWhiteSpace(body.ApiKey))
        {
            var existing = await _repository.GetByProviderKeyAsync("openrouter", cancellationToken);
            if (existing is null || string.IsNullOrWhiteSpace(existing.EncryptedApiKey))
            {
                return BadRequest(ApiResponse<object>.Fail(
                    "Une clé API est requise pour activer OpenRouter."));
            }
        }

        await _repository.UpsertOpenRouterAsync(
            body.DisplayName,
            body.BaseUrl,
            body.ApiKey,
            body.IsEnabled,
            cancellationToken);

        var row = await _repository.GetByProviderKeyAsync("openrouter", cancellationToken);
        var response = MapOpenRouter(row);
        return Ok(ApiResponse<OpenRouterProviderSettingsResponse>.Ok(response));
    }

    private OpenRouterProviderSettingsResponse MapOpenRouter(TenantAiProvider? row)
    {
        var configured = row is not null && !string.IsNullOrEmpty(row.EncryptedApiKey);
        string? last4 = null;
        if (configured)
        {
            try
            {
                var plain = _protector.Unprotect(row!.EncryptedApiKey);
                if (!string.IsNullOrEmpty(plain) && plain.Length >= 4)
                    last4 = plain[^4..];
            }
            catch
            {
                last4 = null;
            }
        }

        return new OpenRouterProviderSettingsResponse
        {
            ProviderKey = "openrouter",
            DisplayName = row?.DisplayName,
            BaseUrl = row?.BaseUrl,
            DefaultBaseUrl = _defaults.DefaultBaseUrl,
            IsEnabled = row?.IsEnabled ?? false,
            IsApiKeyConfigured = configured && last4 is not null,
            ApiKeyLast4 = last4
        };
    }
}

public sealed class OpenRouterProviderSettingsResponse
{
    public string ProviderKey { get; init; } = "openrouter";
    public string? DisplayName { get; init; }
    public string? BaseUrl { get; init; }
    public string DefaultBaseUrl { get; init; } = "";
    public bool IsEnabled { get; init; }
    public bool IsApiKeyConfigured { get; init; }
    public string? ApiKeyLast4 { get; init; }
}

public sealed class UpdateOpenRouterProviderRequest
{
    public string? DisplayName { get; init; }
    public string? BaseUrl { get; init; }
    public string? ApiKey { get; init; }
    public bool IsEnabled { get; init; }
}
