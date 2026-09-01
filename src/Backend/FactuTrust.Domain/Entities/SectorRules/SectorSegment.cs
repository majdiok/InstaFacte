using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.SectorRules;

/// <summary>
/// Master-DB backed segment row (Phase 2, plan §WP-B1) — editable counterpart of the static
/// <c>SegmentDefinition</c> record. The static catalog remains the permanent fallback; this table
/// is only consulted when <c>Features:RegistrationSector:UseDbRules</c> is <c>true</c>.
/// </summary>
public sealed class SectorSegment : Entity
{
    public string Code { get; private set; } = null!;
    public string LabelFr { get; private set; } = null!;
    public string DescriptionFr { get; private set; } = null!;
    public string IconKey { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public string? DefaultWarehouseName { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// True when this row is owned by the catalog seeder (as opposed to an admin-authored/edited
    /// row). Startup reconciliation only ever refreshes/reactivates/deactivates catalog-owned rows;
    /// an admin edit flips this to false via <see cref="MarkAdminManaged"/> so it survives restarts.
    /// </summary>
    public bool IsManagedByCatalog { get; private set; } = true;

    private SectorSegment() { }

    public static SectorSegment Create(
        string code,
        string labelFr,
        string descriptionFr,
        string iconKey,
        int sortOrder,
        string? defaultWarehouseName)
    {
        return new SectorSegment
        {
            Code = code,
            LabelFr = labelFr,
            DescriptionFr = descriptionFr,
            IconKey = iconKey,
            SortOrder = sortOrder,
            DefaultWarehouseName = defaultWarehouseName,
            IsActive = true
        };
    }

    public void UpdateDetails(string labelFr, string descriptionFr, string iconKey, int sortOrder, string? defaultWarehouseName)
    {
        LabelFr = labelFr;
        DescriptionFr = descriptionFr;
        IconKey = iconKey;
        SortOrder = sortOrder;
        DefaultWarehouseName = defaultWarehouseName;
    }

    /// <summary>Resets catalog-owned fields to a catalog value (used by the force-seed path only).</summary>
    public void ResetFromCatalog(string labelFr, string descriptionFr, string iconKey, int sortOrder, string? defaultWarehouseName)
    {
        UpdateDetails(labelFr, descriptionFr, iconKey, sortOrder, defaultWarehouseName);
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    /// <summary>Marks the row as admin-authored/edited so startup reconciliation never overwrites it.</summary>
    public void MarkAdminManaged() => IsManagedByCatalog = false;

    /// <summary>Reclaims the row as catalog-owned (used by a factory-reset force seed).</summary>
    public void MarkCatalogManaged() => IsManagedByCatalog = true;
}
