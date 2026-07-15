namespace FactuTrust.Domain.Entities.Studio;

/// <summary>
/// Per-tenant, per-field counter backing an <c>AutoNumber</c> custom field. One row per
/// (tenant, entity, field key). The value is reserved atomically (Serializable transaction) when a
/// record is created, mirroring the document-numbering scheme used for invoices. New table; isolated
/// from the rest of the schema.
/// </summary>
public sealed class CustomFieldSequence
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EntityDefinitionId { get; private set; }

    /// <summary>The AutoNumber field's machine key (sanitized at design time).</summary>
    public string FieldKey { get; private set; } = null!;

    /// <summary>Last value handed out (0 before the first reservation).</summary>
    public long CurrentValue { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    private CustomFieldSequence() { }

    public static CustomFieldSequence Create(Guid tenantId, Guid entityDefinitionId, string fieldKey)
    {
        return new CustomFieldSequence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityDefinitionId = entityDefinitionId,
            FieldKey = fieldKey,
            CurrentValue = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Increments the counter and returns the newly reserved value (1-based).</summary>
    public long ReserveNext()
    {
        CurrentValue += 1;
        UpdatedAt = DateTime.UtcNow;
        return CurrentValue;
    }
}
