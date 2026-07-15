using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Features.AI;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.AI;

/// <summary>
/// Reads / writes the platform-wide AI model setting. The table
/// <c>PlatformAiSettings</c> holds a single row, created on first use.
/// </summary>
public sealed class PlatformAiSettingsService : IPlatformAiSettingsService
{
    private const string CacheKeyDefaultModel = "platform:ai:default-model";
    private const string CacheKeyImportModel = "platform:ai:import-model";
    private const string CacheKeyInferenceDevice = "platform:ai:inference-device";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly MasterDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PlatformAiSettingsService> _logger;

    public PlatformAiSettingsService(
        MasterDbContext db,
        IMemoryCache cache,
        ILogger<PlatformAiSettingsService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public Task<string?> GetDefaultModelRefAsync(CancellationToken cancellationToken = default) =>
        _cache.GetOrCreateAsync(CacheKeyDefaultModel, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await _db.PlatformAiSettings
                .AsNoTracking()
                .Select(s => s.DefaultModelRef)
                .FirstOrDefaultAsync(cancellationToken);
        });

    public async Task<string?> SetDefaultModelRefAsync(
        string? modelRef,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var normalized = string.IsNullOrWhiteSpace(modelRef)
            ? null
            : ModelRef.NormalizeStored(modelRef);

        var current = await _db.PlatformAiSettings.FirstOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            current = PlatformAiSettings.CreateDefaults();
            current.SetDefaultModel(normalized);
            current.SetAuditInfo(actorUserId.ToString());
            _db.PlatformAiSettings.Add(current);
        }
        else
        {
            current.SetDefaultModel(normalized);
            current.SetAuditInfo(actorUserId.ToString(), isUpdate: true);
        }

        await _db.SaveChangesAsync(cancellationToken);
        InvalidateReadCache();
        return current.DefaultModelRef;
    }

    public Task<string?> GetInvoiceImportModelRefAsync(CancellationToken cancellationToken = default) =>
        _cache.GetOrCreateAsync(CacheKeyImportModel, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            try
            {
                return await _db.PlatformAiSettings
                    .AsNoTracking()
                    .Select(s => s.InvoiceImportModelRef)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is SqlException or DbUpdateException or InvalidOperationException)
            {
                _logger.LogWarning(ex,
                    "Impossible de lire InvoiceImportModelRef depuis PlatformAiSettings ; fallback configuration serveur.");
                return null;
            }
        });

    public async Task<string?> SetInvoiceImportModelRefAsync(
        string? modelRef,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var normalized = string.IsNullOrWhiteSpace(modelRef)
            ? null
            : ModelRef.NormalizeStored(modelRef);

        var current = await _db.PlatformAiSettings.FirstOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            current = PlatformAiSettings.CreateDefaults();
            current.SetInvoiceImportModel(normalized);
            current.SetAuditInfo(actorUserId.ToString());
            _db.PlatformAiSettings.Add(current);
        }
        else
        {
            current.SetInvoiceImportModel(normalized);
            current.SetAuditInfo(actorUserId.ToString(), isUpdate: true);
        }

        await _db.SaveChangesAsync(cancellationToken);
        InvalidateReadCache();
        return current.InvoiceImportModelRef;
    }

    public Task<OllamaInferenceDevice> GetInferenceDeviceAsync(CancellationToken cancellationToken = default) =>
        _cache.GetOrCreateAsync(CacheKeyInferenceDevice, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            try
            {
                var device = await _db.PlatformAiSettings
                    .AsNoTracking()
                    .Select(s => (OllamaInferenceDevice?)s.InferenceDevice)
                    .FirstOrDefaultAsync(cancellationToken);

                return device ?? OllamaInferenceDevice.Gpu;
            }
            catch (Exception ex) when (ex is SqlException or DbUpdateException or InvalidOperationException)
            {
                _logger.LogWarning(ex,
                    "Impossible de lire InferenceDevice depuis PlatformAiSettings ; fallback GPU.");
                return OllamaInferenceDevice.Gpu;
            }
        });

    public async Task<OllamaInferenceDevice> SetInferenceDeviceAsync(
        OllamaInferenceDevice device,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(device))
            throw new ArgumentOutOfRangeException(nameof(device), device, "Valeur InferenceDevice invalide.");

        var current = await _db.PlatformAiSettings.FirstOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            current = PlatformAiSettings.CreateDefaults();
            current.SetInferenceDevice(device);
            current.SetAuditInfo(actorUserId.ToString());
            _db.PlatformAiSettings.Add(current);
        }
        else
        {
            current.SetInferenceDevice(device);
            current.SetAuditInfo(actorUserId.ToString(), isUpdate: true);
        }

        await _db.SaveChangesAsync(cancellationToken);
        InvalidateReadCache();
        return current.InferenceDevice;
    }

    private void InvalidateReadCache()
    {
        _cache.Remove(CacheKeyDefaultModel);
        _cache.Remove(CacheKeyImportModel);
        _cache.Remove(CacheKeyInferenceDevice);
    }
}