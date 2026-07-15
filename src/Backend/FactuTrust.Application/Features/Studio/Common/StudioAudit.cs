using FactuTrust.Application.Common.Interfaces.Services;

namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>
/// Best-effort audit logging for Studio structural mutations. Audit must never break a mutation,
/// so failures are swallowed (the record is already persisted in its own context).
/// </summary>
public static class StudioAudit
{
    public static async Task SafeLogAsync(
        IAuditService audit, string action, string entityType, Guid? id, object? oldValues, object? newValues, CancellationToken cancellationToken)
    {
        try
        {
            await audit.LogAsync(action, entityType, id, oldValues, newValues, cancellationToken);
        }
        catch
        {
            // Best-effort: never fail a Studio mutation because the audit chain write failed.
        }
    }
}
