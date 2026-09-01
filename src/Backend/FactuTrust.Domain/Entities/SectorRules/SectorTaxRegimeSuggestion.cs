using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.SectorRules;

/// <summary>
/// Master-DB backed segment → suggested tax regime row (Phase 3, plan §3.1): a richer,
/// admin-editable superseder of the minimal <c>UsualTaxRegimeCatalog</c> (plan §2.4). Purely
/// informational — surfaced as <c>SectorCatalogDto.SuggestedTaxRegimes</c> so the registration
/// wizard can pre-select / suggest the usual regime for a segment and explain *why* in French.
/// Natural key: <c>(SegmentCode, Regime)</c>.
/// </summary>
public sealed class SectorTaxRegimeSuggestion : Entity
{
    public string SegmentCode { get; private set; } = null!;
    public int Regime { get; private set; }
    public string NoteFr { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// True when this row is owned by the catalog seeder. Startup reconciliation only ever
    /// refreshes/reactivates/deactivates catalog-owned rows; an admin edit flips this to false.
    /// </summary>
    public bool IsManagedByCatalog { get; private set; } = true;

    private SectorTaxRegimeSuggestion() { }

    public static SectorTaxRegimeSuggestion Create(string segmentCode, int regime, string noteFr, int sortOrder)
    {
        return new SectorTaxRegimeSuggestion
        {
            SegmentCode = segmentCode,
            Regime = regime,
            NoteFr = noteFr,
            SortOrder = sortOrder,
            IsActive = true
        };
    }

    public void UpdateValue(string noteFr, int sortOrder)
    {
        NoteFr = noteFr;
        SortOrder = sortOrder;
    }

    public void ResetFromCatalog(string noteFr, int sortOrder)
    {
        UpdateValue(noteFr, sortOrder);
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    /// <summary>Marks the row as admin-authored/edited so startup reconciliation never overwrites it.</summary>
    public void MarkAdminManaged() => IsManagedByCatalog = false;

    /// <summary>Reclaims the row as catalog-owned (used by a factory-reset force seed).</summary>
    public void MarkCatalogManaged() => IsManagedByCatalog = true;
}
