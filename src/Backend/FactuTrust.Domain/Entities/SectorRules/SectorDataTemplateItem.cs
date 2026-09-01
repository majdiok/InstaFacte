using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.SectorRules;

/// <summary>
/// One item within a <see cref="SectorDataTemplate"/> (plan §WP-B5/§WP-B6). <see cref="ItemKind"/>
/// is one of <c>document-numbering-scheme</c>, <c>chart-account</c>, <c>setting</c> (unknown kinds
/// are tolerated by the applier as a forward-compatible warning, never a hard failure).
/// <see cref="PayloadJson"/> deserializes to the kind's payload contract.
/// </summary>
public sealed class SectorDataTemplateItem : Entity
{
    public Guid TemplateId { get; private set; }
    public string ItemKind { get; private set; } = null!;
    public string PayloadJson { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// True when this row is owned by the catalog seeder. Startup reconciliation only ever
    /// refreshes/reactivates/deactivates catalog-owned items; an admin edit flips this to false.
    /// </summary>
    public bool IsManagedByCatalog { get; private set; } = true;

    private SectorDataTemplateItem() { }

    public static SectorDataTemplateItem Create(Guid templateId, string itemKind, string payloadJson, int sortOrder)
    {
        return new SectorDataTemplateItem
        {
            TemplateId = templateId,
            ItemKind = itemKind,
            PayloadJson = payloadJson,
            SortOrder = sortOrder,
            IsActive = true
        };
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    /// <summary>Marks the row as admin-authored/edited so startup reconciliation never overwrites it.</summary>
    public void MarkAdminManaged() => IsManagedByCatalog = false;

    /// <summary>Reclaims the row as catalog-owned (used by a factory-reset force seed).</summary>
    public void MarkCatalogManaged() => IsManagedByCatalog = true;

    /// <summary>
    /// Refreshes the payload for a catalog-known item on a force/reconcile seed run (review R3) —
    /// without this, a template <c>Version</c> bump in the catalog would publish a NEW version
    /// number over the SAME OLD account payload, since items are matched by the natural key
    /// <c>(ItemKind, SortOrder)</c> and were previously only ever reactivated, never refreshed.
    /// </summary>
    public void UpdatePayload(string payloadJson) => PayloadJson = payloadJson;
}
