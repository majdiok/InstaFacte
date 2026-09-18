using Hangfire;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.Studio.Workflows;

/// <summary>
/// Job Hangfire d'une définition de workflow planifiée (4.7b2 / D-47-B03) — <b>coquille</b> :
/// le balayage filtré et les démarrages système arrivent en 4.7b3. Enregistré aux écritures par
/// <see cref="StudioWorkflowScheduleService"/> sous l'identifiant
/// <c>studio-workflow-scheduled:{tenantId:N}:{definitionId:N}</c> (cron UTC).
/// </summary>
public sealed class StudioWorkflowScheduledJob
{
    private readonly ILogger<StudioWorkflowScheduledJob> _logger;

    public StudioWorkflowScheduledJob(ILogger<StudioWorkflowScheduledJob> logger) => _logger = logger;

    /// <summary>Corps livré en 4.7b3 (balayage filtré, une instance par enregistrement, auto-retrait).</summary>
    [DisableConcurrentExecution(540)]
    [AutomaticRetry(Attempts = 0)]
    public Task FireAsync(Guid tenantId, Guid definitionId, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Studio workflow planifié {WorkflowId} (tenant {TenantId}) : tick sans effet (corps livré en 4.7b3).",
            definitionId, tenantId);
        return Task.CompletedTask;
    }
}
