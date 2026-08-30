namespace FactuTrust.Domain.SectorConfiguration;

/// <summary>
/// Known "business domain" codes for the sector-aware registration wizard (plan §3 C1, §5).
/// Same string-code rationale as <see cref="CompanySegments"/>.
/// </summary>
public static class BusinessDomains
{
    public const string TechnologieInformatique = "technologie-informatique";
    public const string AlimentationAgroalimentaire = "alimentation-agroalimentaire";
    public const string SanteParamedical = "sante-paramedical";
    public const string TextileHabillement = "textile-habillement";
    public const string TransportLogistique = "transport-logistique";
    public const string Immobilier = "immobilier";
    public const string EnergieEnvironnement = "energie-environnement";
    public const string CommunicationMarketing = "communication-marketing";
    public const string Artisanat = "artisanat";
    public const string Autre = "autre";

    public static readonly IReadOnlyList<string> All = new[]
    {
        TechnologieInformatique,
        AlimentationAgroalimentaire,
        SanteParamedical,
        TextileHabillement,
        TransportLogistique,
        Immobilier,
        EnergieEnvironnement,
        CommunicationMarketing,
        Artisanat,
        Autre
    };

    /// <summary>Trims and lower-invariants a raw code. Null/whitespace ⇒ null.</summary>
    public static string? Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        return code.Trim().ToLowerInvariant();
    }

    /// <summary>True when <paramref name="code"/> (after normalization) matches a known domain.</summary>
    public static bool IsKnown(string? code)
    {
        var normalized = Normalize(code);
        return normalized is not null && All.Contains(normalized, StringComparer.Ordinal);
    }
}
