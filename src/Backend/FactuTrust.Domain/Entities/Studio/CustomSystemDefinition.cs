namespace FactuTrust.Domain.Entities.Studio;

/// <summary>
/// Metadata describing a user-defined multi-table system (Notion-style app) in Studio.
/// Groups related <see cref="CustomEntityDefinition"/> rows under one sidebar entry.
/// </summary>
public sealed class CustomSystemDefinition
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Machine name, sanitized, unique per tenant. Used in URLs/keys.</summary>
    public string Key { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;
    public string? Icon { get; private set; }
    public string? Description { get; private set; }

    /// <summary>JSON array of onboarding step strings for the system hub page.</summary>
    public string? OnboardingJson { get; private set; }

    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    private CustomSystemDefinition() { }

    public static CustomSystemDefinition Create(
        Guid tenantId,
        string key,
        string displayName,
        string? icon,
        string? description,
        string? onboardingJson,
        Guid? createdBy)
    {
        return new CustomSystemDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = key,
            DisplayName = displayName,
            Icon = icon,
            Description = description,
            OnboardingJson = onboardingJson,
            IsActive = true,
            IsDeleted = false,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(string displayName, string? icon, string? description, string? onboardingJson, bool isActive, Guid? updatedBy)
    {
        DisplayName = displayName;
        Icon = icon;
        Description = description;
        OnboardingJson = onboardingJson;
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
