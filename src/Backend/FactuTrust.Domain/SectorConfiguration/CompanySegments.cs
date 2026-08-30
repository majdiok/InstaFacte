namespace FactuTrust.Domain.SectorConfiguration;

/// <summary>
/// Known "company segment" codes for the sector-aware registration wizard (plan §3 C1, §5).
/// Wire/storage format is lowercase-kebab strings (not a C# enum) — see plan decision C1 for
/// rationale: unknown values must degrade to a controlled 400, never an opaque model-binding error,
/// and Phase 2 makes these codes DB-editable without a code deploy.
/// </summary>
public static class CompanySegments
{
    public const string Entreprise = "entreprise";
    public const string Commerce = "commerce";
    public const string Services = "services";
    public const string BtpConstruction = "btp-construction";
    public const string Association = "association";
    public const string EtablissementEducatif = "etablissement-educatif";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Entreprise,
        Commerce,
        Services,
        BtpConstruction,
        Association,
        EtablissementEducatif
    };

    /// <summary>Trims and lower-invariants a raw code. Null/whitespace ⇒ null.</summary>
    public static string? Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        return code.Trim().ToLowerInvariant();
    }

    /// <summary>True when <paramref name="code"/> (after normalization) matches a known segment.</summary>
    public static bool IsKnown(string? code)
    {
        var normalized = Normalize(code);
        return normalized is not null && All.Contains(normalized, StringComparer.Ordinal);
    }
}
