using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.SectorRules;

/// <summary>
/// Master-DB backed default-setting row (Phase 2, plan §WP-B1/§WP-B3): scoped by an optional
/// segment/domain code pair, e.g. <c>('commerce', null, 'default-warehouse-name', 'Magasin principal', 'string')</c>
/// or the global <c>(null, null, 'plan-comptable-variant', 'nct01', 'string')</c> row seeded by
/// <c>SectorRuleSeeder</c>. Purely informational for the registration/reconfiguration flows —
/// consumed by the backoffice and by <c>SectorReconfigurationPreviewDto.Settings</c>.
/// </summary>
public sealed class SectorDefaultSetting : Entity
{
    public string? SegmentCode { get; private set; }
    public string? DomainCode { get; private set; }
    public string SettingKey { get; private set; } = null!;
    public string SettingValue { get; private set; } = null!;
    public string ValueType { get; private set; } = "string";
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// True when this row is owned by the catalog seeder. Startup reconciliation only ever
    /// refreshes/reactivates/deactivates catalog-owned settings; an admin edit flips this to false.
    /// </summary>
    public bool IsManagedByCatalog { get; private set; } = true;

    private SectorDefaultSetting() { }

    public static SectorDefaultSetting Create(
        string? segmentCode,
        string? domainCode,
        string settingKey,
        string settingValue,
        string valueType,
        int sortOrder)
    {
        return new SectorDefaultSetting
        {
            SegmentCode = segmentCode,
            DomainCode = domainCode,
            SettingKey = settingKey,
            SettingValue = settingValue,
            ValueType = valueType,
            SortOrder = sortOrder,
            IsActive = true
        };
    }

    public void UpdateValue(string settingValue, string valueType, int sortOrder)
    {
        SettingValue = settingValue;
        ValueType = valueType;
        SortOrder = sortOrder;
    }

    public void ResetFromCatalog(string settingValue, string valueType, int sortOrder)
    {
        UpdateValue(settingValue, valueType, sortOrder);
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    /// <summary>Marks the row as admin-authored/edited so startup reconciliation never overwrites it.</summary>
    public void MarkAdminManaged() => IsManagedByCatalog = false;

    /// <summary>Reclaims the row as catalog-owned (used by a factory-reset force seed).</summary>
    public void MarkCatalogManaged() => IsManagedByCatalog = true;
}
