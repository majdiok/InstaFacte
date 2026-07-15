using System.Text.Json;

namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>(De)serialization for report definitions.</summary>
public static class ReportDefinitionJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(ReportDefinition definition) => JsonSerializer.Serialize(definition, Options);

    public static ReportDefinition Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ReportDefinition();
        try { return JsonSerializer.Deserialize<ReportDefinition>(json, Options) ?? new ReportDefinition(); }
        catch (JsonException) { return new ReportDefinition(); }
    }
}
