using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Domain.SectorConfiguration;

/// <summary>
/// Non-blocking coherence check between the NIF's taxpayer category (letter at position 9 of the
/// NIF, e.g. the "L" in NNNNNNN/L/A/M/NNN — see <see cref="NIF.TaxpayerCategory"/> /
/// <see cref="TaxpayerCategories"/>) and the <c>CompanySegment</c> chosen during registration
/// (plan §3.2). Deliberately narrow and fail-open, mirroring <see cref="UsualTaxRegimeCatalog"/>:
/// only flags the one clearly-incoherent case the plan calls out (association category vs a
/// non-association segment, and vice versa). An unknown/unmapped combination never produces a
/// warning — never a false positive.
/// </summary>
public static class NifCategorySegmentCoherenceChecker
{
    /// <summary>
    /// Returns a French, human-readable warning when the NIF's taxpayer category and the chosen
    /// segment look incoherent, or <c>null</c> when there is nothing to report (including when
    /// <paramref name="segmentCode"/> is absent/unknown — fail open).
    /// </summary>
    public static string? CheckCoherence(NIF? nif, string? segmentCode)
    {
        if (nif is null)
            return null;

        var normalizedSegment = CompanySegments.Normalize(segmentCode);
        if (normalizedSegment is null || !CompanySegments.IsKnown(normalizedSegment))
            return null;

        var category = nif.TaxpayerCategory;
        var isAssociationCategory = category == TaxpayerCategories.Association;
        var isAssociationSegment = string.Equals(normalizedSegment, CompanySegments.Association, StringComparison.OrdinalIgnoreCase);

        if (isAssociationCategory && !isAssociationSegment)
        {
            return "Votre NIF indique une association (catégorie D), mais le segment sélectionné n'est pas « Association ». " +
                   "Vérifiez votre choix de segment.";
        }

        if (isAssociationSegment && !isAssociationCategory && category != ' ')
        {
            return $"Le segment sélectionné est « Association », mais votre NIF indique la catégorie {category} " +
                   $"({TaxpayerCategories.GetDescription(category)}) et non une association. " +
                   "Vérifiez votre choix de segment ou votre NIF.";
        }

        return null;
    }
}
