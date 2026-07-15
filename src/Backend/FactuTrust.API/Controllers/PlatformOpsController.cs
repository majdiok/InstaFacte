using FactuTrust.API.Authorization;
using FactuTrust.Domain.Auth;
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Lot B5 — Endpoints d'observabilité plateforme.
///
/// Expose un résumé synthétique du health-check + statistiques Hangfire pour
/// alimenter la future page <c>/ops/health</c> du backoffice. Les endpoints
/// sont strictement réservés aux administrateurs plateforme.
/// </summary>
[ApiController]
[Route("api/platform/ops")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformOpsController : ControllerBase
{
    private readonly HealthCheckService _healthChecks;

    public PlatformOpsController(HealthCheckService healthChecks)
    {
        _healthChecks = healthChecks;
    }

    /// <summary>Snapshot consolidé : health checks + queues Hangfire.</summary>
    [HttpGet("health")]
    [Authorize(Policy = "perm:" + PlatformPermissions.SecurityRead)]
    [ProducesResponseType(typeof(ApiResponse<OpsHealthDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Health(CancellationToken cancellationToken)
    {
        var report = await _healthChecks.CheckHealthAsync(cancellationToken);

        var checks = report.Entries.Select(e => new OpsHealthCheckDto
        {
            Name = e.Key,
            Status = e.Value.Status.ToString(),
            DurationMs = (int)e.Value.Duration.TotalMilliseconds,
            Description = e.Value.Description,
            Tags = e.Value.Tags.ToList()
        }).ToList();

        // Hangfire stats — best-effort, reste tolérant si Hangfire n'est pas démarré.
        OpsHangfireDto? hangfire = null;
        try
        {
            var monitoring = JobStorage.Current.GetMonitoringApi();
            var stats = monitoring.GetStatistics();
            hangfire = new OpsHangfireDto
            {
                ServersOnline = (int)stats.Servers,
                Enqueued = (int)stats.Enqueued,
                Scheduled = (int)stats.Scheduled,
                Processing = (int)stats.Processing,
                Succeeded = (int)stats.Succeeded,
                Failed = (int)stats.Failed,
                Recurring = (int)stats.Recurring
            };
        }
        catch
        {
            // Hangfire n'est pas configuré ou indisponible → on rapporte null sans crash
        }

        var dto = new OpsHealthDto
        {
            Status = report.Status.ToString(),
            TotalDurationMs = (int)report.TotalDuration.TotalMilliseconds,
            Checks = checks,
            Hangfire = hangfire
        };

        return Ok(ApiResponse<OpsHealthDto>.Ok(dto));
    }
}

public sealed record OpsHealthDto
{
    public string Status { get; init; } = null!;
    public int TotalDurationMs { get; init; }
    public IReadOnlyList<OpsHealthCheckDto> Checks { get; init; } = Array.Empty<OpsHealthCheckDto>();
    public OpsHangfireDto? Hangfire { get; init; }
}

public sealed record OpsHealthCheckDto
{
    public string Name { get; init; } = null!;
    public string Status { get; init; } = null!;
    public int DurationMs { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}

public sealed record OpsHangfireDto
{
    public int ServersOnline { get; init; }
    public int Enqueued { get; init; }
    public int Scheduled { get; init; }
    public int Processing { get; init; }
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public int Recurring { get; init; }
}
