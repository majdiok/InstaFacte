using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Studio;

/// <summary>
/// Append-only log of one automation execution against one record: its outcome, the ERP result
/// (e.g. the created invoice id) for link-back, and any error. Enables idempotency ("already ran"),
/// retry, and an audit trail of bridge activity.
/// </summary>
public sealed class CustomAutomationRun
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid AutomationId { get; private set; }
    public Guid RecordId { get; private set; }

    public StudioAutomationRunStatus Status { get; private set; }

    /// <summary>Serialized ERP result of the action (e.g. <c>{ "invoiceId": "…" }</c>), for link-back.</summary>
    public string? ResultJson { get; private set; }

    public string? Error { get; private set; }

    /// <summary>Optional key to deduplicate runs of the same logical action.</summary>
    public string? IdempotencyKey { get; private set; }

    public DateTime RunAt { get; private set; }
    public Guid? RunBy { get; private set; }

    private CustomAutomationRun() { }

    public static CustomAutomationRun Create(
        Guid tenantId, Guid automationId, Guid recordId, StudioAutomationRunStatus status,
        string? resultJson, string? error, string? idempotencyKey, Guid? runBy)
    {
        return new CustomAutomationRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AutomationId = automationId,
            RecordId = recordId,
            Status = status,
            ResultJson = resultJson,
            Error = error,
            IdempotencyKey = idempotencyKey,
            RunAt = DateTime.UtcNow,
            RunBy = runBy
        };
    }
}
