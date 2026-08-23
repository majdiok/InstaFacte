namespace FactuTrust.Application.Features.Search;

public enum SearchEntityType
{
    Invoice,
    Quote,
    DeliveryNote,
    Client,
    Product,
    Supplier,
    SalesReturnNote
}

public interface IGlobalSearchProvider
{
    SearchEntityType EntityType { get; }
    string RequiredPermission { get; }
    Task<IReadOnlyList<GlobalSearchProviderResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken);
}

public sealed record GlobalSearchProviderResult
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string? Status { get; init; }
    public DateTime? Date { get; init; }
    public required string DetailRoute { get; init; }
    public required string ListRoute { get; init; }
    public required string Icon { get; init; }
    public int Score { get; init; }
}

internal static class SearchScoring
{
    public static int ScoreMatch(string query, params string?[] fields)
    {
        var normalizedQuery = Normalize(query);
        if (string.IsNullOrEmpty(normalizedQuery)) return 0;
        var best = 0;
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field)) continue;
            var normalizedField = Normalize(field);
            if (normalizedField == normalizedQuery) best = Math.Max(best, 100);
            else if (normalizedField.StartsWith(normalizedQuery, StringComparison.Ordinal)) best = Math.Max(best, 80);
            else if (normalizedField.Contains(normalizedQuery, StringComparison.Ordinal)) best = Math.Max(best, 50);
        }
        return best;
    }
    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
