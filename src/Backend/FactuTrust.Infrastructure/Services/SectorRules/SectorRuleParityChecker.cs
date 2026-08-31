using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;

namespace FactuTrust.Infrastructure.Services.SectorRules;

/// <summary>
/// Phase 2 — machine-checkable proof that the DB rule set reproduces the static catalog before the
/// <c>UseDbRules</c> flag flips (plan §WP-B9). Compares two <see cref="SectorRuleSnapshot"/>s for
/// structural equality across every observable surface a tenant sees at registration time:
/// segment/domain sets, labels, sort orders, default warehouse names, the segment↔domain link lists,
/// and — for all <c>6 segments × (10 domains + null) = 66 combos</c> — the resolved
/// <see cref="SectorProfile"/> (core/recommended/optional modules + warehouse). Returns
/// <see cref="IsMatch"/> + a flat <see cref="Differences"/> list (one difference string per detected
/// divergence) so the platform UI can show exactly what drifted.
/// </summary>
public static class SectorRuleParityChecker
{
    /// <summary>
    /// Compares the two snapshots and returns a parity result. <paramref name="staticSnap"/> is the
    /// reference (the permanent static catalog); <paramref name="dbSnap"/> is the candidate (the
    /// live master-DB rule tables, read directly — never the composite provider).
    /// </summary>
    public static SectorRuleParityResult Check(SectorRuleSnapshot staticSnap, SectorRuleSnapshot dbSnap)
    {
        var differences = new List<string>();

        CompareSegments(staticSnap, dbSnap, differences);
        CompareDomains(staticSnap, dbSnap, differences);
        CompareProfiles(staticSnap, dbSnap, differences);

        return new SectorRuleParityResult
        {
            IsMatch = differences.Count == 0,
            Differences = differences
        };
    }

    // ---------- segment / domain catalog rows ----------

    private static void CompareSegments(SectorRuleSnapshot staticSnap, SectorRuleSnapshot dbSnap, List<string> differences)
    {
        var staticByCode = staticSnap.Segments.ToDictionary(s => s.Code, StringComparer.Ordinal);
        var dbByCode = dbSnap.Segments.ToDictionary(s => s.Code, StringComparer.Ordinal);

        CompareKeys(staticByCode.Keys, dbByCode.Keys, "segment", differences);

        foreach (var code in staticByCode.Keys.Intersect(dbByCode.Keys, StringComparer.Ordinal))
        {
            var s = staticByCode[code];
            var d = dbByCode[code];

            if (!string.Equals(s.LabelFr, d.LabelFr, StringComparison.Ordinal))
                differences.Add($"segment[{code}]: libellé attendu «{s.LabelFr}», obtenu «{d.LabelFr}».");

            if (!string.Equals(s.DescriptionFr, d.DescriptionFr, StringComparison.Ordinal))
                differences.Add($"segment[{code}]: description attendue «{s.DescriptionFr}», obtenue «{d.DescriptionFr}».");

            if (!string.Equals(s.IconKey, d.IconKey, StringComparison.Ordinal))
                differences.Add($"segment[{code}]: icône attendue «{s.IconKey}», obtenue «{d.IconKey}».");

            if (s.SortOrder != d.SortOrder)
                differences.Add($"segment[{code}]: ordre attendu {s.SortOrder}, obtenu {d.SortOrder}.");

            if (!string.Equals(s.DefaultWarehouseName, d.DefaultWarehouseName, StringComparison.Ordinal))
                differences.Add($"segment[{code}]: entrepôt par défaut attendu «{s.DefaultWarehouseName}», obtenu «{d.DefaultWarehouseName}».");

            CompareModuleSet(s.BaseRecommendedModules, d.BaseRecommendedModules,
                $"segment[{code}]: modules recommandés de base", differences);

            // Phase 1 semantics: every domain is available to every segment. A DB segment stripped of
            // all links resolves unrestricted (defensive fallback), so an EMPTY db link list still
            // matches the static "all domains" behavior — only a NON-empty mismatch differs.
            var staticDomainCodes = s.DomainCodes.OrderBy(c => c, StringComparer.Ordinal).ToList();
            var dbDomainCodes = d.DomainCodes.OrderBy(c => c, StringComparer.Ordinal).ToList();
            if (dbDomainCodes.Count > 0 && !staticDomainCodes.SequenceEqual(dbDomainCodes, StringComparer.Ordinal))
                differences.Add($"segment[{code}]: domaines liés attendus [{string.Join(",", staticDomainCodes)}], obtenus [{string.Join(",", dbDomainCodes)}].");
        }
    }

    private static void CompareDomains(SectorRuleSnapshot staticSnap, SectorRuleSnapshot dbSnap, List<string> differences)
    {
        var staticByCode = staticSnap.Domains.ToDictionary(d => d.Code, StringComparer.Ordinal);
        var dbByCode = dbSnap.Domains.ToDictionary(d => d.Code, StringComparer.Ordinal);

        CompareKeys(staticByCode.Keys, dbByCode.Keys, "domaine", differences);

        foreach (var code in staticByCode.Keys.Intersect(dbByCode.Keys, StringComparer.Ordinal))
        {
            var s = staticByCode[code];
            var d = dbByCode[code];

            if (!string.Equals(s.LabelFr, d.LabelFr, StringComparison.Ordinal))
                differences.Add($"domaine[{code}]: libellé attendu «{s.LabelFr}», obtenu «{d.LabelFr}».");

            if (s.SortOrder != d.SortOrder)
                differences.Add($"domaine[{code}]: ordre attendu {s.SortOrder}, obtenu {d.SortOrder}.");

            CompareModuleSet(s.OverlayModules, d.OverlayModules,
                $"domaine[{code}]: modules d'overlay", differences);
        }
    }

    // ---------- resolved profiles for all 66 combos ----------

    private static void CompareProfiles(SectorRuleSnapshot staticSnap, SectorRuleSnapshot dbSnap, List<string> differences)
    {
        // 6 segments × (10 domains + null domain) = 66 combos.
        var domainCodes = BusinessDomains.All.ToList();

        foreach (var segmentCode in CompanySegments.All)
        {
            // Combo with no domain first.
            CompareOneProfile(staticSnap, dbSnap, segmentCode, domainCode: null, differences);

            foreach (var domainCode in domainCodes)
                CompareOneProfile(staticSnap, dbSnap, segmentCode, domainCode, differences);
        }
    }

    private static void CompareOneProfile(
        SectorRuleSnapshot staticSnap,
        SectorRuleSnapshot dbSnap,
        string segmentCode,
        string? domainCode,
        List<string> differences)
    {
        var staticProfile = staticSnap.Resolve(segmentCode, domainCode);
        var dbProfile = dbSnap.Resolve(segmentCode, domainCode);

        var combo = domainCode is null ? segmentCode : $"{segmentCode}+{domainCode}";

        if (staticProfile is null && dbProfile is null)
            return;

        if (staticProfile is null || dbProfile is null)
        {
            var expected = staticProfile is null ? "null" : "profil";
            var obtained = dbProfile is null ? "null" : "profil";
            differences.Add($"{combo}: profil attendu {expected}, obtenu {obtained}.");
            return;
        }

        CompareModuleSet(staticProfile.RecommendedModules, dbProfile.RecommendedModules,
            $"{combo}: recommandé", differences);

        CompareModuleSet(staticProfile.OptionalModules, dbProfile.OptionalModules,
            $"{combo}: optionnel", differences);

        if (!string.Equals(staticProfile.DefaultWarehouseName, dbProfile.DefaultWarehouseName, StringComparison.Ordinal))
            differences.Add($"{combo}: entrepôt attendu «{staticProfile.DefaultWarehouseName}», obtenu «{dbProfile.DefaultWarehouseName}».");
    }

    // ---------- shared helpers ----------

    private static void CompareKeys(
        IEnumerable<string> expected,
        IEnumerable<string> actual,
        string label,
        List<string> differences)
    {
        var expectedSet = new HashSet<string>(expected, StringComparer.Ordinal);
        var actualSet = new HashSet<string>(actual, StringComparer.Ordinal);

        foreach (var missing in expectedSet.Except(actualSet).OrderBy(c => c, StringComparer.Ordinal))
            differences.Add($"{label}[{missing}] attendu, manquant en BDD.");

        foreach (var extra in actualSet.Except(expectedSet).OrderBy(c => c, StringComparer.Ordinal))
            differences.Add($"{label}[{extra}] présent en BDD, absent du catalogue statique.");
    }

    /// <summary>
    /// Compares two module sets as sorted id lists and records a single flat difference string on
    /// mismatch (e.g. <c>"commerce+artisanat: recommandé attendu [7,8,12], obtenu [7,8]"</c>).
    /// </summary>
    private static void CompareModuleSet(
        IReadOnlyList<AppModule> expected,
        IReadOnlyList<AppModule> actual,
        string label,
        List<string> differences)
    {
        var expectedIds = expected.Select(m => (int)m).OrderBy(m => m).ToList();
        var actualIds = actual.Select(m => (int)m).OrderBy(m => m).ToList();

        if (expectedIds.SequenceEqual(actualIds) is false)
            differences.Add($"{label} attendu [{string.Join(",", expectedIds)}], obtenu [{string.Join(",", actualIds)}].");
    }
}

/// <summary>Outcome of <see cref="SectorRuleParityChecker.Check"/> (plan §WP-B9).</summary>
public sealed record SectorRuleParityResult
{
    /// <summary>True when no observable divergence was detected between the static and DB snapshots.</summary>
    public required bool IsMatch { get; init; }

    /// <summary>Flat, human-readable difference strings (empty when <see cref="IsMatch"/> is true).</summary>
    public required IReadOnlyList<string> Differences { get; init; }
}
