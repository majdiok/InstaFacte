namespace FactuTrust.Domain.Entities.Studio;

/// <summary>
/// A data-entry form layout over a custom entity. <see cref="LayoutJson"/> stores the ordered
/// field keys, sections, widths, and label overrides. Each entity has one default form
/// (<see cref="IsDefault"/>) auto-generated from its fields; named forms can be added later.
/// </summary>
public sealed class CustomFormDefinition
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EntityDefinitionId { get; private set; }

    /// <summary>Unique per tenant.</summary>
    public string Key { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    /// <summary>Serialized form layout (sections, ordered field keys, widths, label overrides).</summary>
    public string LayoutJson { get; private set; } = null!;

    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }

    public Guid? CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    private CustomFormDefinition() { }

    public static CustomFormDefinition Create(
        Guid tenantId,
        Guid entityDefinitionId,
        string key,
        string displayName,
        string layoutJson,
        bool isDefault,
        Guid? createdBy)
    {
        return new CustomFormDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityDefinitionId = entityDefinitionId,
            Key = key,
            DisplayName = displayName,
            LayoutJson = layoutJson,
            IsDefault = isDefault,
            IsActive = true,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(string displayName, string layoutJson, bool isActive, Guid? updatedBy)
    {
        DisplayName = displayName;
        LayoutJson = layoutJson;
        IsActive = isActive;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
