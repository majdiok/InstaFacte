using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>
/// Sequential Modal credential resolution: tenant override, else platform singleton.
/// Never uses Task.WhenAll on the scoped MasterDbContext.
/// </summary>
public sealed class ModalCredentialsResolver : IModalCredentialsResolver
{
    private readonly MasterDbContext _db;
    private readonly IPlatformAiSettingsService _platformAiSettings;
    private readonly IDataProtector _protector;
    private readonly ModalSettings _modalDefaults;
    private readonly ILogger<ModalCredentialsResolver> _logger;

    public ModalCredentialsResolver(
        MasterDbContext db,
        IPlatformAiSettingsService platformAiSettings,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<ModalSettings> modalSettings,
        ILogger<ModalCredentialsResolver> logger)
    {
        _db = db;
        _platformAiSettings = platformAiSettings;
        _protector = dataProtectionProvider.CreateProtector(TenantModalSettingsService.DataProtectionPurpose);
        _modalDefaults = modalSettings.Value;
        _logger = logger;
    }

    public async Task<ResolvedModalCredentials> ResolveAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId is null)
            return ResolvedModalCredentials.FromPlatform(
                await _platformAiSettings.GetModalCredentialsAsync(cancellationToken));

        var row = await _db.TenantModalSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value, cancellationToken);

        if (row is null)
            return ResolvedModalCredentials.FromPlatform(
                await _platformAiSettings.GetModalCredentialsAsync(cancellationToken));

        return MapOverride(row);
    }

    private ResolvedModalCredentials MapOverride(TenantModalSettings row)
    {
        var baseUrl = ResolveDefaultBaseUrl();
        if (!string.IsNullOrWhiteSpace(row.BaseUrl)
            && ModalEndpointUrl.TryNormalize(row.BaseUrl, out var stored, out _)
            && !string.IsNullOrWhiteSpace(stored))
        {
            baseUrl = stored;
        }

        if (!row.IsEnabled)
            return new ResolvedModalCredentials(false, null, baseUrl, ModalCredentialSource.Disabled);

        if (string.IsNullOrWhiteSpace(row.EncryptedApiKey))
            return new ResolvedModalCredentials(true, null, baseUrl, ModalCredentialSource.TenantOverride);

        try
        {
            var plain = _protector.Unprotect(row.EncryptedApiKey);
            if (string.IsNullOrWhiteSpace(plain))
                return new ResolvedModalCredentials(true, null, baseUrl, ModalCredentialSource.TenantOverride);
            return new ResolvedModalCredentials(true, plain, baseUrl, ModalCredentialSource.TenantOverride);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Échec du déchiffrement de la clé Modal du tenant {TenantId}.", row.TenantId);
            return new ResolvedModalCredentials(true, null, baseUrl, ModalCredentialSource.TenantOverride);
        }
    }

    private string ResolveDefaultBaseUrl()
    {
        if (ModalEndpointUrl.TryNormalize(_modalDefaults.DefaultBaseUrl, out var normalized, out _)
            && !string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return string.Empty;
    }
}
