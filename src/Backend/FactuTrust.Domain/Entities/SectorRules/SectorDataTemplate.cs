using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.SectorRules;

/// <summary>
/// Master-DB backed data template header (Phase 2, plan §WP-B5/§WP-B6, §4.3): a named, versioned
/// set of <see cref="SectorDataTemplateItem"/> rows applied additively to a tenant DB when its
/// segment/domain matches (null = applies to every segment/domain). The static catalog declares a
/// small set of additive chart-of-accounts presets (<c>SectorConfigurationCatalog.DataTemplates</c>),
/// seeded by <c>SectorRuleSeeder</c>; additional templates can be authored via the admin CRUD.
/// </summary>
public sealed class SectorDataTemplate : Entity
{
    public string Code { get; private set; } = null!;
    public string? SegmentCode { get; private set; }
    public string? DomainCode { get; private set; }
    public string LabelFr { get; private set; } = null!;
    public string? DescriptionFr { get; private set; }
    public int Version { get; private set; } = 1;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// True when this row is owned by the catalog seeder. Startup reconciliation only ever
    /// refreshes/reactivates/deactivates catalog-owned templates; an admin edit flips this to false.
    /// </summary>
    public bool IsManagedByCatalog { get; private set; } = true;

    private SectorDataTemplate() { }

    public static SectorDataTemplate Create(
        string code,
        string? segmentCode,
        string? domainCode,
        string labelFr,
        string? descriptionFr,
        int version,
        int sortOrder)
    {
        return new SectorDataTemplate
        {
            Code = code,
            SegmentCode = segmentCode,
            DomainCode = domainCode,
            LabelFr = labelFr,
            DescriptionFr = descriptionFr,
            Version = version,
            SortOrder = sortOrder,
            IsActive = true
        };
    }

    public void UpdateDetails(string labelFr, string? descriptionFr, int version, int sortOrder)
    {
        LabelFr = labelFr;
        DescriptionFr = descriptionFr;
        Version = version;
        SortOrder = sortOrder;
    }

    /// <summary>
    /// Refreshes the segment/domain scope a catalog-known template targets (review R3). Only the
    /// catalog seeder needs this — <c>UpdateDetails</c> (used by the admin CRUD's PUT endpoint too)
    /// intentionally leaves the scope untouched, since the admin surface never lets an operator
    /// re-target an existing template to a different segment/domain.
    /// </summary>
    public void UpdateScope(string? segmentCode, string? domainCode)
    {
        SegmentCode = segmentCode;
        DomainCode = domainCode;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    /// <summary>Marks the row as admin-authored/edited so startup reconciliation never overwrites it.</summary>
    public void MarkAdminManaged() => IsManagedByCatalog = false;

    /// <summary>Reclaims the row as catalog-owned (used by a factory-reset force seed).</summary>
    public void MarkCatalogManaged() => IsManagedByCatalog = true;
}
