using FactuTrust.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.Background;

/// <summary>
/// Lot C1 — Job Hangfire de purge des overrides de modules expirés.
///
/// Programmé quotidiennement à 02h UTC dans <c>Program.cs</c>. Idempotent.
/// </summary>
public sealed class ExpireModuleOverridesJob
{
    private readonly ITenantModuleOverrideService _service;
    private readonly ILogger<ExpireModuleOverridesJob> _logger;

    public ExpireModuleOverridesJob(
        ITenantModuleOverrideService service,
        ILogger<ExpireModuleOverridesJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var deleted = await _service.RemoveExpiredAsync(cancellationToken);
        _logger.LogInformation("ExpireModuleOverridesJob: removed {Count} expired overrides", deleted);
    }
}
