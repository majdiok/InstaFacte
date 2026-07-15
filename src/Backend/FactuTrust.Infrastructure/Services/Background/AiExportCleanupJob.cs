using FactuTrust.Application.Features.AI.Export.PowerPoint.Services;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.Background;

/// <summary>
/// Removes expired AI export artefacts (PowerPoint .pptx, manifests) from disk. Programmed via
/// <c>RecurringJob.AddOrUpdate</c> in <c>Program.cs</c>. The job is intentionally lightweight: it
/// only iterates the manifest files for each tenant folder, so it stays bounded even with many
/// tenants.
/// </summary>
public sealed class AiExportCleanupJob
{
    private readonly IExportStorageService _storage;
    private readonly ILogger<AiExportCleanupJob> _logger;

    public AiExportCleanupJob(IExportStorageService storage, ILogger<AiExportCleanupJob> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var deleted = await _storage.CleanupExpiredAsync(cancellationToken);
            _logger.LogInformation("AI export cleanup completed (removed {Count} files).", deleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI export cleanup job failed.");
            throw; // let Hangfire schedule a retry
        }
    }
}
