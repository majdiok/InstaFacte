using FactuTrust.Domain.Entities;

namespace FactuTrust.Domain.SectorConfiguration;

/// <summary>
/// Minimal segment → "usual" tax regimes lookup (plan §2.4 point 3 — basis for the non-blocking
/// warning surfaced by <c>CompanyController.UpdateCompany</c> when the tenant picks an atypical
/// regime for its segment). Deliberately small and hand-picked, not DB-editable: the richer,
/// admin-editable <c>suggestedTaxRegimes</c> rule table described in plan §3.1 supersedes this once
/// built. An unknown segment code is never considered atypical (fail open — no false warning).
/// </summary>
public static class UsualTaxRegimeCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<TaxRegime>> UsualRegimesBySegment =
        new Dictionary<string, IReadOnlyCollection<TaxRegime>>(StringComparer.OrdinalIgnoreCase)
        {
            ["entreprise"] = new[] { TaxRegime.RealRegime },
            ["commerce"] = new[] { TaxRegime.RealRegime, TaxRegime.FlatRateRegime },
            ["services"] = new[] { TaxRegime.RealRegime, TaxRegime.FlatRateRegime },
            ["btp-construction"] = new[] { TaxRegime.RealRegime },
            ["association"] = new[] { TaxRegime.Exempt },
            ["etablissement-educatif"] = new[] { TaxRegime.Exempt, TaxRegime.RealRegime }
        };

    /// <summary>True when <paramref name="regime"/> is not among the usual regimes for <paramref name="segmentCode"/>.</summary>
    public static bool IsAtypical(string? segmentCode, TaxRegime regime)
    {
        if (string.IsNullOrWhiteSpace(segmentCode))
            return false;

        return UsualRegimesBySegment.TryGetValue(segmentCode, out var usual) && !usual.Contains(regime);
    }
}
