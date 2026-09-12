using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Studio;

/// <summary>
/// Metadata describing a user-defined "table" (custom entity) created in the low-code Studio.
/// Notion-style: this is just a definition row; the actual records live as JSON in
/// <see cref="CustomRecord"/>. No physical SQL table is ever created at runtime.
/// Calquée sur <c>UserDashboardLayout</c> (entité tenant simple, RowVersion, soft-delete).
/// </summary>
public sealed class CustomEntityDefinition
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Machine name, sanitized (<c>^[a-z][a-z0-9_]{1,63}$</c>), unique per tenant. Used in URLs/keys.</summary>
    public string Key { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;
    public string DisplayNamePlural { get; private set; } = null!;

    /// <summary>FontAwesome/PrimeIcons class for the sidebar entry (nullable).</summary>
    public string? Icon { get; private set; }
    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Reserved for future first-party/system entities; user-created ones are false.</summary>
    public bool IsSystem { get; private set; }

    /// <summary>Optional parent system grouping (Notion-style multi-table apps).</summary>
    public Guid? SystemId { get; private set; }

    /// <summary>
    /// Nature de l'entité (<see cref="CustomEntityKind.Standard"/> par défaut). Une
    /// <see cref="CustomEntityKind.Junction"/> porte une relation plusieurs‑à‑plusieurs ; la valeur
    /// est fixée à la création et n'est pas modifiable par <see cref="Update"/>.
    /// </summary>
    public CustomEntityKind Kind { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    private CustomEntityDefinition() { }

    public static CustomEntityDefinition Create(
        Guid tenantId,
        string key,
        string displayName,
        string displayNamePlural,
        string? icon,
        string? description,
        Guid? createdBy,
        Guid? systemId = null,
        CustomEntityKind kind = CustomEntityKind.Standard)
    {
        return new CustomEntityDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = key,
            DisplayName = displayName,
            DisplayNamePlural = displayNamePlural,
            Icon = icon,
            Description = description,
            IsActive = true,
            IsSystem = false,
            SystemId = systemId,
            Kind = kind,
            IsDeleted = false,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void AssignToSystem(Guid? systemId, Guid? updatedBy)
    {
        SystemId = systemId;
        Touch(updatedBy);
    }

    public void Update(string displayName, string displayNamePlural, string? icon, string? description, bool isActive, Guid? updatedBy)
    {
        DisplayName = displayName;
        DisplayNamePlural = displayNamePlural;
        Icon = icon;
        Description = description;
        IsActive = isActive;
        Touch(updatedBy);
    }

    public void SoftDelete(Guid? updatedBy)
    {
        IsDeleted = true;
        IsActive = false;
        DeletedAt = DateTime.UtcNow;
        Touch(updatedBy);
    }

    private void Touch(Guid? updatedBy)
    {
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
