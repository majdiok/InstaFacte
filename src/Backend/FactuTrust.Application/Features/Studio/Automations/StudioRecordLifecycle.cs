using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Studio.Automations;

/// <summary>
/// Publishes record-lifecycle notifications for the ERP bridge. The publish is wrapped so that a
/// failing/absent automation handler can NEVER affect the record's create/update result (the record
/// is already persisted before this is called).
/// </summary>
public static class StudioRecordLifecycle
{
    public static async Task PublishAsync(
        IPublisher publisher, Guid tenantId, Guid entityDefinitionId, Guid recordId, string dataJson,
        StudioAutomationTrigger trigger, Guid? runBy, CancellationToken cancellationToken)
    {
        try
        {
            await publisher.Publish(
                new CustomRecordLifecycleNotification(tenantId, entityDefinitionId, recordId, dataJson, trigger, runBy),
                cancellationToken);
        }
        catch
        {
            // Best-effort: bridge automations must never break the Studio record save.
        }
    }
}
