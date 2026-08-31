using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Tracks which sector data templates (plan §WP-B6) have already been applied to this tenant
/// database, keyed by <see cref="TemplateCode"/> + <see cref="Version"/>. Additive-only: a template
/// re-applies (its items are re-evaluated, check-before-insert) only when its catalog/DB
/// <see cref="Version"/> increases; the same (code, version) pair is never re-applied. Rows are
/// never deleted — disabling Phase 2 simply stops new applications, it does not undo prior ones
/// (plan D10).
/// </summary>
public sealed class AppliedSectorTemplate : Entity
{
    public string TemplateCode { get; private set; } = null!;
    public int Version { get; private set; }
    public DateTime AppliedAtUtc { get; private set; }

    private AppliedSectorTemplate() { }

    public static AppliedSectorTemplate Create(string templateCode, int version)
    {
        return new AppliedSectorTemplate
        {
            TemplateCode = templateCode,
            Version = version,
            AppliedAtUtc = DateTime.UtcNow
        };
    }
}
