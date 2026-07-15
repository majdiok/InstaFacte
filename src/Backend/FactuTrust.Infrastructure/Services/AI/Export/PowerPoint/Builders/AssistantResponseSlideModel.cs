namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

/// <summary>
/// Internal model that captures the structured content extracted from a single assistant message
/// (markdown text, KPI cards, tables, charts and source tools). The orchestrator hydrates this
/// model once and passes it to every interested builder so parsing happens only one time per
/// response.
/// </summary>
public sealed record AssistantResponseSlideModel(
    Guid ConversationId,
    Guid MessageId,
    string Title,
    DateTime CreatedAt,
    string? MarkdownText,
    string? DashboardTitle,
    IReadOnlyList<DashboardKpiBlock> Kpis,
    IReadOnlyList<DashboardTableBlock> Tables,
    IReadOnlyList<DashboardChartBlock> Charts,
    IReadOnlyList<string> SourceTools,
    IReadOnlyList<MarkdownSectionModel>? Sections = null)
{
    public IReadOnlyList<MarkdownSectionModel> Sections { get; init; } =
        Sections ?? Array.Empty<MarkdownSectionModel>();
}

public sealed record MarkdownSectionModel(
    string Title,
    string Content,
    MarkdownSectionKind Kind);

public enum MarkdownSectionKind
{
    Generic,
    ExecutiveSummary,
    KeyIndicators,
    DetailedAnalysis,
    Risks,
    Opportunities,
    Actions,
    DataLimits
}

public sealed record DashboardKpiBlock(string? Label, string? Value, string? Trend, string? Unit);

public sealed record DashboardTableBlock(
    string? Title,
    IReadOnlyList<DashboardTableColumn> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows);

public sealed record DashboardTableColumn(string Key, string Label);

public sealed record DashboardChartBlock(
    string? Title,
    string ChartType,
    IReadOnlyList<string> Labels,
    IReadOnlyList<DashboardChartSeries> Series);

public sealed record DashboardChartSeries(string Label, IReadOnlyList<double?> Values);
