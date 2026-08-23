using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>CRUD for per-tenant Modal overrides in the master database.</summary>
public sealed class TenantModalSettingsService : ITenantModalSettingsService
{
    public const string DataProtectionPurpose = "TenantModalSecrets";

    private readonly MasterDbContext _db;
    private readonly IPlatformAiSettingsService _platformAiSettings;
    private readonly IDataProtector _protector;
    private readonly ModalSettings _modalDefaults;
    private readonly ILogger<TenantModalSettingsService> _logger;

    public TenantModalSettingsService(
        MasterDbContext db,
        IPlatformAiSettingsService platformAiSettings,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<ModalSettings> modalSettings,
        ILogger<TenantModalSettingsService> logger)
    {
        _db = db;
        _platformAiSettings = platformAiSettings;
        _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
        _modalDefaults = modalSettings.Value;
        _logger = logger;
    }

    public async Task<Result<TenantModalSettingsDto>> GetAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!await TenantExistsAsync(tenantId, cancellationToken))
            return Result.Failure<TenantModalSettingsDto>(Error.NotFound("Tenant", tenantId));

        var row = await _db.TenantModalSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

        return Result.Success(await MapDtoAsync(row, cancellationToken));
    }

    public async Task<Result<TenantModalSettingsDto>> SetAsync(
        Guid tenantId,
        UpdateTenantModalSettingsRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await TenantExistsAsync(tenantId, cancellationToken))
            return Result.Failure<TenantModalSettingsDto>(Error.NotFound("Tenant", tenantId));

        string? normalizedUrl = null;
        if (!string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            if (!ModalEndpointUrl.TryNormalize(request.BaseUrl, out normalizedUrl, out var urlError))
                return Result.Failure<TenantModalSettingsDto>(
                    Error.Validation("BaseUrl", urlError ?? "URL Modal invalide."));
        }

        var current = await _db.TenantModalSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        var isNew = current is null;
        current ??= TenantModalSettings.Create(tenantId);

        var urlToStore = normalizedUrl ?? current.BaseUrl;

        if (request.IsEnabled)
        {
            var effectiveUrl = urlToStore ?? _modalDefaults.DefaultBaseUrl;
            if (!ModalEndpointUrl.TryNormalize(effectiveUrl, out var checkedUrl, out var enableUrlError)
                || string.IsNullOrWhiteSpace(checkedUrl))
            {
                return Result.Failure<TenantModalSettingsDto>(Error.Validation(
                    "BaseUrl",
                    enableUrlError ?? "Une URL de base HTTPS (se terminant par /v1) est requise pour activer Modal."));
            }

            urlToStore = checkedUrl;
        }

        string? encrypted = null;
        string? last4 = null;
        var hasNewKey = !string.IsNullOrWhiteSpace(request.ApiKey);
        if (hasNewKey)
        {
            var plain = request.ApiKey!.Trim();
            encrypted = _protector.Protect(plain);
            last4 = plain.Length >= 4 ? plain[^4..] : plain;
        }
        else if (request.IsEnabled && !current.HasModalApiKey)
        {
            return Result.Failure<TenantModalSettingsDto>(Error.Validation(
                "ApiKey",
                "Une clé API (TOKEN_ID.TOKEN_SECRET) est requise pour activer Modal."));
        }

        current.SetModalConfig(
            request.IsEnabled,
            request.DisplayName,
            urlToStore,
            hasNewKey ? encrypted : null,
            hasNewKey ? last4 : null);
        current.SetAuditInfo(actorUserId.ToString(), isUpdate: !isNew);

        if (isNew)
            _db.TenantModalSettings.Add(current);

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapDtoAsync(current, cancellationToken));
    }

    public async Task<Result<TenantModalSettingsDto>> DeleteAsync(
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await TenantExistsAsync(tenantId, cancellationToken))
            return Result.Failure<TenantModalSettingsDto>(Error.NotFound("Tenant", tenantId));

        var current = await _db.TenantModalSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (current is not null)
        {
            _db.TenantModalSettings.Remove(current);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Platform admin {ActorId} removed tenant Modal override (tenant={TenantId})",
                actorUserId,
                tenantId);
        }

        return Result.Success(await MapDtoAsync(row: null, cancellationToken));
    }

    private async Task<bool> TenantExistsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await _db.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, cancellationToken);

    private async Task<TenantModalSettingsDto> MapDtoAsync(
        TenantModalSettings? row,
        CancellationToken cancellationToken)
    {
        var platform = await _platformAiSettings.GetModalSettingsAsync(cancellationToken);
        var platformModel = await _platformAiSettings.GetDefaultModelRefAsync(cancellationToken);
        var defaultBase = ResolveDefaultBaseUrl();
        var defaultModel = string.IsNullOrWhiteSpace(_modalDefaults.DefaultModelId)
            ? "moonshotai/Kimi-K3"
            : _modalDefaults.DefaultModelId.Trim();

        var configured = row is not null
            && !string.IsNullOrWhiteSpace(row.EncryptedApiKey)
            && !string.IsNullOrWhiteSpace(row.ApiKeyLast4);

        var snapshot = new TenantModalPlatformSnapshotDto(
            platform.IsEnabled,
            platform.DisplayName,
            platform.BaseUrl,
            platform.IsApiKeyConfigured,
            platform.IsApiKeyConfigured ? platform.ApiKeyLast4 : null);

        return new TenantModalSettingsDto(
            HasOverride: row is not null,
            IsEnabled: row?.IsEnabled ?? false,
            DisplayName: row?.DisplayName,
            BaseUrl: row?.BaseUrl,
            DefaultBaseUrl: defaultBase,
            DefaultModelId: defaultModel,
            IsApiKeyConfigured: configured,
            ApiKeyLast4: configured ? row!.ApiKeyLast4 : null,
            PlatformConfiguredModelRef: platformModel,
            Platform: snapshot);
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
