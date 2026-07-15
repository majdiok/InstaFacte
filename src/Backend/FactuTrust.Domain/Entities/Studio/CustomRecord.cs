namespace FactuTrust.Domain.Entities.Studio;

/// <summary>
/// One data row of a custom entity, stored as a JSON document keyed by field <c>Key</c>.
/// All custom records of all custom entities share this single physical table, scoped by
/// (TenantId, EntityDefinitionId). This is the Notion-style storage that avoids runtime DDL.
/// </summary>
public sealed class CustomRecord
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EntityDefinitionId { get; private set; }

    /// <summary><c>{ "fieldKey": value, ... }</c> — validated and canonicalized by the application layer before persisting.</summary>
    public string DataJson { get; private set; } = null!;

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    private CustomRecord() { }

    public static CustomRecord Create(Guid tenantId, Guid entityDefinitionId, string dataJson, Guid? createdBy)
    {
        return new CustomRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityDefinitionId = entityDefinitionId,
            DataJson = dataJson,
            IsDeleted = false,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void SetData(string dataJson, Guid? updatedBy)
    {
        DataJson = dataJson;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete(Guid? updatedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
