using System.Text.Json;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Studio.Common;

public sealed record ViewColumnFormatOptions
{
    public Dictionary<string, string>? StatusMap { get; init; }
    public string? LookupTable { get; init; }
    public string? LookupDisplayColumn { get; init; }
    public string? DateFormat { get; init; }
}

public sealed record ViewColumn
{
    public string Name { get; init; } = string.Empty;
    public string? Label { get; init; }
    public string? Width { get; init; } // full | half
    public string? Format { get; init; } // text|date|datetime|number|money|boolean|uuid|status|fk
    public ViewColumnFormatOptions? FormatOptions { get; init; }
}

public sealed record ViewDefinition
{
    public IReadOnlyList<ViewColumn> Columns { get; init; } = Array.Empty<ViewColumn>();
    public bool Search { get; init; }
}

public sealed record CustomViewDto(
    Guid Id,
    string Key,
    string DisplayName,
    string SourceTable,
    ViewDefinition Definition,
    bool IsActive);

public sealed record SaveCustomViewRequest(
    string? Key,
    string DisplayName,
    string SourceTable,
    ViewDefinition Definition);

public static class ViewDefinitionJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(ViewDefinition definition) => JsonSerializer.Serialize(definition, Options);

    public static ViewDefinition Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ViewDefinition();
        try { return JsonSerializer.Deserialize<ViewDefinition>(json, Options) ?? new ViewDefinition(); }
        catch (JsonException) { return new ViewDefinition(); }
    }
}
