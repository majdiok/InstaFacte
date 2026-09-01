namespace FactuTrust.Domain.Entities;

/// <summary>
/// Master-DB audit trail for module-grant changes made outside the registration wizard (plan §2.2 —
/// <c>CompanyModulesController</c>). Records who changed what and when. Deliberately simpler than
/// the tenant-DB hash-chained <c>AuditLog</c> (used for in-app, tenant-scoped actions such as
/// invoices/payments): this table lives in the master DB because module grants themselves
/// (<see cref="UserModuleGrant"/>) are master-DB rows, and there is no existing master-DB audit
/// precedent to extend — a full hash chain is not warranted for this lower-stakes, admin-facing
/// action.
/// </summary>
public sealed class ModuleGrantAuditEntry
{
    public Guid Id { get; private set; }

    /// <summary>Tenant whose module grants changed.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>User id of the admin who made the change. Null if the change was system-initiated.</summary>
    public Guid? ActorUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Short discriminator, e.g. "company-modules-update".</summary>
    public string Action { get; private set; } = null!;

    /// <summary>
    /// JSON diff — no PII: e.g. <c>{"requestedIds":[...],"enabledIds":[...],"deniedByPlan":[...],"affectedUserIds":[...]}</c>.
    /// </summary>
    public string DiffJson { get; private set; } = null!;

    private ModuleGrantAuditEntry() { }

    public static ModuleGrantAuditEntry Create(Guid tenantId, Guid? actorUserId, string action, string diffJson)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action is required.", nameof(action));

        return new ModuleGrantAuditEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ActorUserId = actorUserId,
            CreatedAtUtc = DateTime.UtcNow,
            Action = action.Trim(),
            DiffJson = diffJson ?? "{}"
        };
    }
}
