namespace FactuTrust.Domain.Entities.Studio;

/// <summary>
/// A saved READ-ONLY view over an existing SQL Server table: a selection of columns (with optional
/// label overrides / order) presented in a list. Data is never modified through a view. Columns are
/// re-validated against the live schema at execution time.
/// </summary>
public sealed class CustomViewDefinition
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Unique per tenant.</summary>
    public string Key { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    /// <summary>The existing tenant table name (validated against the live schema + denylist on every run).</summary>
    public string SourceTable { get; private set; } = null!;

    /// <summary>Serialized <c>{ columns: [{ name, label, width }], search? }</c>.</summary>
    public string DefinitionJson { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public Guid? CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    private CustomViewDefinition() { }

    public static CustomViewDefinition Create(
        Guid tenantId, string key, string displayName, string sourceTable, string definitionJson, Guid? createdBy)
    {
        return new CustomViewDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = key,
            DisplayName = displayName,
            SourceTable = sourceTable,
            DefinitionJson = definitionJson,
            IsActive = true,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(string displayName, string sourceTable, string definitionJson, bool isActive, Guid? updatedBy)
    {
        DisplayName = displayName;
        SourceTable = sourceTable;
        DefinitionJson = definitionJson;
        IsActive = isActive;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
