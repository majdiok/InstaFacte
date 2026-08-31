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
}
