using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.SectorRules;

/// <summary>Distinguishes a segment's base recommendation from a domain's overlay (plan §WP-B1).</summary>
public enum SectorModuleRuleKind
{
    /// <summary>Module recommended by the segment alone (mirrors <c>SegmentDefinition.BaseRecommendedModules</c>).</summary>
    SegmentBase = 0,

    /// <summary>Module added on top of the segment base when a domain is selected (mirrors <c>DomainDefinition.OverlayModules</c>).</summary>
    DomainOverlay = 1
}

/// <summary>
/// Master-DB backed recommended-module row (Phase 2, plan §WP-B1). Exactly one of
/// <see cref="SegmentId"/>/<see cref="DomainId"/> is set, matching <see cref="RuleKind"/>.
/// </summary>
public sealed class SectorModuleRule : Entity
{
    public SectorModuleRuleKind RuleKind { get; private set; }
    public Guid? SegmentId { get; private set; }
    public Guid? DomainId { get; private set; }
    public int ModuleId { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// True when this row is owned by the catalog seeder. Startup reconciliation only ever
    /// refreshes/reactivates/deactivates catalog-owned rules; an admin edit flips this to false.
    /// </summary>
    public bool IsManagedByCatalog { get; private set; } = true;

    private SectorModuleRule() { }

    public static SectorModuleRule CreateSegmentBase(Guid segmentId, int moduleId, int sortOrder)
    {
        return new SectorModuleRule
        {
            RuleKind = SectorModuleRuleKind.SegmentBase,
            SegmentId = segmentId,
            DomainId = null,
            ModuleId = moduleId,
            SortOrder = sortOrder,
            IsActive = true
        };
    }

    public static SectorModuleRule CreateDomainOverlay(Guid domainId, int moduleId, int sortOrder)
    {
        return new SectorModuleRule
        {
            RuleKind = SectorModuleRuleKind.DomainOverlay,
            SegmentId = null,
            DomainId = domainId,
            ModuleId = moduleId,
            SortOrder = sortOrder,
            IsActive = true
        };
    }

    public void UpdateSortOrder(int sortOrder) => SortOrder = sortOrder;

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    /// <summary>Marks the row as admin-authored/edited so startup reconciliation never overwrites it.</summary>
    public void MarkAdminManaged() => IsManagedByCatalog = false;

    /// <summary>Reclaims the row as catalog-owned (used by a factory-reset force seed).</summary>
    public void MarkCatalogManaged() => IsManagedByCatalog = true;
}
