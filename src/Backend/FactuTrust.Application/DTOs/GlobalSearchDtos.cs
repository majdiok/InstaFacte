namespace FactuTrust.Application.DTOs;

public sealed record GlobalSearchResultDto
{
    public required string EntityType { get; init; }
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string? Status { get; init; }
    public DateTime? Date { get; init; }
    public required string Route { get; init; }
    public required string ListRoute { get; init; }
    public required string Icon { get; init; }
    public int Score { get; init; }
}

public sealed record GlobalSearchResponseDto
{
    public required string Query { get; init; }
    public IReadOnlyList<GlobalSearchResultDto> Results { get; init; } = Array.Empty<GlobalSearchResultDto>();
    public IReadOnlyList<string> TruncatedTypes { get; init; } = Array.Empty<string>();
}
