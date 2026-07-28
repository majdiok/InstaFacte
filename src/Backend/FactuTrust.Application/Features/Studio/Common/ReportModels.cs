using System.Text.Json.Nodes;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>
/// A report definition over a data source. For grouped reports, <see cref="Grouping"/> +
/// <see cref="Aggregations"/> drive the output; for detail reports (no grouping), <see cref="Fields"/>
/// selects the projected columns. Serialized to <c>CustomReportDefinition.DefinitionJson</c>.
/// </summary>
public sealed record ReportDefinition
{
    public IReadOnlyList<string> Fields { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ReportFilter> Filters { get; init; } = Array.Empty<ReportFilter>();
    public IReadOnlyList<string> Grouping { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ReportAggregation> Aggregations { get; init; } = Array.Empty<ReportAggregation>();
    public IReadOnlyList<ReportSort> Sort { get; init; } = Array.Empty<ReportSort>();
}

/// <summary>op ∈ eq, neq, gt, gte, lt, lte, contains, in, between.</summary>
public sealed record ReportFilter
{
    public string Field { get; init; } = string.Empty;
    public string Op { get; init; } = "eq";
    public JsonNode? Value { get; init; }
    public JsonNode? Value2 { get; init; } // for 'between'
}

/// <summary>fn ∈ sum, avg, count, min, max.</summary>
public sealed record ReportAggregation
{
    public string Field { get; init; } = string.Empty;
    public string Fn { get; init; } = "count";
}

public sealed record ReportSort
{
    public string Field { get; init; } = string.Empty;
    public string Dir { get; init; } = "asc";
}

// ---- Result ----

/// <summary>Lightweight field descriptor for the report runner (decoupled from custom vs existing sources).</summary>
public sealed record ReportFieldMeta(string Key, string Label, bool Numeric);

public sealed record ReportColumn(string Key, string Label, string Kind); // kind: dimension | measure

public sealed record ReportResultDto(
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    int TotalRows);

// ---- API DTOs ----

public sealed record CustomReportDto(
    Guid Id,
    string Key,
    string DisplayName,
    CustomReportDataSourceKind DataSourceKind,
    string DataSourceRef,
    ReportDefinition Definition,
    bool IsActive);

public sealed record SaveCustomReportRequest(
    string? Key,
    string DisplayName,
    CustomReportDataSourceKind DataSourceKind,
    string DataSourceRef,
    ReportDefinition Definition);

public sealed record RunReportPreviewRequest(
    CustomReportDataSourceKind DataSourceKind,
    string DataSourceRef,
    ReportDefinition Definition);

/// <summary>A selectable report data source (custom entity or whitelisted existing source) + its fields.</summary>
public sealed record ReportSourceDto(string Kind, string Ref, string DisplayName, IReadOnlyList<ReportFieldMeta> Fields);

/// <summary>
/// Tout ce qu'il faut pour imprimer un état Studio : en-tête société, titre, description de la
/// définition (filtres/tris résumés) et le résultat déjà exécuté. Le rendu est générique — il ne
/// connaît que <see cref="ReportResultDto"/>, donc tout état Studio devient imprimable sans code dédié.
/// </summary>
public sealed record StudioReportPdfContext(
    string CompanyName,
    string? MatriculeFiscal,
    string Title,
    string? SourceLabel,
    IReadOnlyList<string> Criteria,
    ReportResultDto Result);
