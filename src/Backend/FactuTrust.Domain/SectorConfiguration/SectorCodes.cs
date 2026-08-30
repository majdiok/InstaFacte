namespace FactuTrust.Domain.SectorConfiguration;

/// <summary>
/// Shared normalization/lookup helper for the wire-format string codes used by
/// <see cref="CompanySegments"/> and <see cref="BusinessDomains"/> (plan §3 C1, §5) — both catalogs
/// normalize (trim + lower-invariant) and look up "is this code known" the same way, so the logic
/// lives here once instead of being duplicated in each catalog.
/// </summary>
internal static class SectorCodes
{
    /// <summary>Trims and lower-invariants a raw code. Null/whitespace ⇒ null.</summary>
    public static string? Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        return code.Trim().ToLowerInvariant();
    }

    /// <summary>True when <paramref name="code"/> (after normalization) is present in <paramref name="knownCodes"/>.</summary>
    public static bool IsKnown(string? code, IReadOnlyList<string> knownCodes)
    {
        var normalized = Normalize(code);
        return normalized is not null && knownCodes.Contains(normalized, StringComparer.Ordinal);
    }
}
