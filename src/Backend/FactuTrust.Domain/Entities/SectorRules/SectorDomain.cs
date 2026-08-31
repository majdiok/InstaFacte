using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.SectorRules;

/// <summary>
/// Master-DB backed domain row (Phase 2, plan §WP-B1) — editable counterpart of the static
/// <c>DomainDefinition</c> record.
/// </summary>
public sealed class SectorDomain : Entity
{
    public string Code { get; private set; } = null!;
    public string LabelFr { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    private SectorDomain() { }

    public static SectorDomain Create(string code, string labelFr, int sortOrder)
    {
        return new SectorDomain
        {
            Code = code,
            LabelFr = labelFr,
            SortOrder = sortOrder,
            IsActive = true
        };
    }

    public void UpdateDetails(string labelFr, int sortOrder)
    {
        LabelFr = labelFr;
        SortOrder = sortOrder;
    }

    public void ResetFromCatalog(string labelFr, int sortOrder)
    {
        UpdateDetails(labelFr, sortOrder);
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
