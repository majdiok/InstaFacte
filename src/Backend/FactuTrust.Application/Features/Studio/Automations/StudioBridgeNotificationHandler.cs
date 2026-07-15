using System.Text.Json;
using System.Text.Json.Nodes;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.Studio.Automations;

/// <summary>
/// Runs the active ERP-bridge automations matching a record-lifecycle event. Fully best-effort: a
/// failing automation is logged and recorded, never propagated — the record's save is never affected
/// (mirrors the cash-operation → accounting notification pattern).
/// </summary>
public sealed class StudioBridgeNotificationHandler : INotificationHandler<CustomRecordLifecycleNotification>
{
    private readonly ICustomAutomationRepository _repo;
    private readonly IStudioBridgeExecutor _executor;
    private readonly ILogger<StudioBridgeNotificationHandler> _logger;

    public StudioBridgeNotificationHandler(
        ICustomAutomationRepository repo, IStudioBridgeExecutor executor, ILogger<StudioBridgeNotificationHandler> logger)
    {
        _repo = repo;
        _executor = executor;
        _logger = logger;
    }

    public async Task Handle(CustomRecordLifecycleNotification n, CancellationToken cancellationToken)
    {
        IReadOnlyList<Domain.Entities.Studio.CustomEntityAutomation> automations;
        try
        {
            automations = await _repo.ListActiveByTriggerAsync(n.TenantId, n.EntityDefinitionId, n.Trigger, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load bridge automations for entity {EntityId}", n.EntityDefinitionId);
            return;
        }

        if (automations.Count == 0) return;

        var data = ParseObject(n.DataJson);

        foreach (var automation in automations)
        {
            try
            {
                if (automation.RunOnce && await _repo.HasSuccessfulRunAsync(n.TenantId, automation.Id, n.RecordId, cancellationToken))
                    continue;

                await _executor.ExecuteAsync(automation, n.TenantId, n.RecordId, data, n.RunBy, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bridge automation {AutomationId} failed for record {RecordId}", automation.Id, n.RecordId);
            }
        }
    }

    private static JsonObject? ParseObject(string json)
    {
        try { return JsonNode.Parse(json) as JsonObject; }
        catch (JsonException) { return null; }
    }
}
