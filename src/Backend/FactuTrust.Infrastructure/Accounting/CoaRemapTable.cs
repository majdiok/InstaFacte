namespace FactuTrust.Infrastructure.Accounting;

public sealed record CoaRemapEntry(string From, string? To, string Mode, bool Prefix, string Reason);

/// <summary>
/// Table de correspondance PCG hybride → NCT 01. <see cref="Rewrite"/> est une passe unique
/// (le plus long <c>from</c> gagne), ce qui résout les cycles 231↔232 et 421↔425 sans tampon
/// sur les colonnes métier. Le tampon n'est requis que pour l'unicité de ChartOfAccounts.
/// </summary>
public sealed class CoaRemapTable
{
    public CoaRemapTable(
        string version,
        IReadOnlyList<string> protectedPrefixes,
        IReadOnlyList<CoaRemapEntry> entries)
    {
        Version = version;
        ProtectedPrefixes = protectedPrefixes;
        Entries = entries;
        _rewriteEntries = entries
            .Where(e => e.To is not null
                        && (e.Mode == "move" || e.Mode == "merge-into")
                        && !string.Equals(e.From, e.To, StringComparison.Ordinal))
            .OrderByDescending(e => e.From.Length)
            .ToArray();
    }

    public string Version { get; }
    public IReadOnlyList<string> ProtectedPrefixes { get; }
    public IReadOnlyList<CoaRemapEntry> Entries { get; }

    private readonly CoaRemapEntry[] _rewriteEntries;

    public string Rewrite(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return accountNumber ?? string.Empty;

        if (IsProtected(accountNumber))
            return accountNumber;

        foreach (var entry in _rewriteEntries)
        {
            if (accountNumber.Equals(entry.From, StringComparison.Ordinal))
                return entry.To!;

            if (entry.Prefix && accountNumber.StartsWith(entry.From, StringComparison.Ordinal))
                return entry.To + accountNumber[entry.From.Length..];
        }

        return accountNumber;
    }

    public bool IsProtected(string accountNumber) =>
        ProtectedPrefixes.Any(p =>
            accountNumber.Equals(p, StringComparison.Ordinal)
            || accountNumber.StartsWith(p, StringComparison.Ordinal));

    public IReadOnlyList<CoaRemapEntry> DeactivateEntries =>
        Entries.Where(e => e.Mode == "deactivate").ToList();
}
