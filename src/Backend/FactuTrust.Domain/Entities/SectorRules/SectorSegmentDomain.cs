using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.SectorRules;

/// <summary>
/// Join row: which <see cref="SectorDomain"/>s are available/suggested for a given
/// <see cref="SectorSegment"/> (Phase 2, plan §WP-B1/§WP-B4). Seeded from the catalog's §3.1 matrix
/// (<c>SegmentDefinition.AllowedDomainCodes</c>) by <c>SectorRuleSeeder</c> — only the declared
/// segment↔domain pairs, not every combination — and then editable — an admin can prune the list
/// further, which activates the WP-B4 400 validation.
/// </summary>
public sealed class SectorSegmentDomain : Entity
{
    public Guid SegmentId { get; private set; }
    public Guid DomainId { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    private SectorSegmentDomain() { }

    public static SectorSegmentDomain Create(Guid segmentId, Guid domainId, int sortOrder)
    {
        return new SectorSegmentDomain
        {
            SegmentId = segmentId,
            DomainId = domainId,
            SortOrder = sortOrder,
            IsActive = true
        };
    }

    public void UpdateSortOrder(int sortOrder) => SortOrder = sortOrder;

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
