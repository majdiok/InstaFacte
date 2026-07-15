using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;

/// <summary>
/// Extracts the structured content (markdown text, KPI cards, tables, charts and source tools)
/// from a persisted assistant <see cref="ConversationMessage"/>. The parser mirrors the frontend
/// rendering logic (<c>assistant-message-display.ts</c>) so both UIs stay consistent.
/// </summary>
internal static class AssistantResponseParser
{
    private static readonly Regex DashboardJsonFenceRegex = new(
        "```json\\s*([\\s\\S]*?)\\s*```",
        RegexOptions.Compiled);

    private static readonly Regex FtMetaFenceRegex = new(
        "```ft-meta\\s*([\\s\\S]*?)\\s*```",
        RegexOptions.Compiled);

    private static readonly Regex CodeFenceRegex = new(
        "```[\\s\\S]*?```",
        RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static AssistantResponseSlideModel? Parse(
        Conversation conversation,
        Guid messageId,
        string? customTitle,
        CultureInfo culture)
    {
        var message = conversation.Messages.FirstOrDefault(m => m.Id == messageId);
        if (message is null || message.Role != MessageRole.Assistant)
            return null;

        var rawContent = message.Content ?? string.Empty;

        var dashboard = ExtractDashboardConfig(rawContent);
        var (markdown, dashboardTitle) = StripPersistenceFences(rawContent, dashboard?.RawFence);

        var (kpis, tables, charts) = ExtractDashboardBlocks(dashboard?.Config, culture);

        var sources = ExtractSourceTools(conversation, message);

        var title = SanitizeTitle(customTitle)
            ?? SanitizeTitle(dashboardTitle)
            ?? DeriveTitleFromMarkdown(markdown)
            ?? "Réponse de l'assistant";

        return new AssistantResponseSlideModel(
            ConversationId: conversation.Id,
            MessageId: message.Id,
            Title: title,
            CreatedAt: message.CreatedAt,
            MarkdownText: markdown,
            DashboardTitle: dashboardTitle,
            Kpis: kpis,
            Tables: tables,
            Charts: charts,
            SourceTools: sources);
    }

    private static (string Markdown, string? DashboardTitle) StripPersistenceFences(string raw, string? dashboardRawFence)
    {
        var stripped = FtMetaFenceRegex.Replace(raw, string.Empty);

        // Remove every JSON fence that is not the dashboard fence. The dashboard fence is removed
        // separately so we can still expose the dashboard title.
        stripped = DashboardJsonFenceRegex.Replace(stripped, match =>
        {
            var inner = match.Groups[1].Value.Trim();
            if (TryParseDashboard(inner, out _))
                return string.Empty; // dashboard fence removed
            return string.Empty;
        });

        if (!string.IsNullOrEmpty(dashboardRawFence))
        {
            stripped = stripped.Replace(dashboardRawFence, string.Empty, StringComparison.Ordinal);
        }

        // Collapse triple+ newlines and trim.
        stripped = Regex.Replace(stripped, @"\n{3,}", "\n\n").Trim();

        return (stripped, null);
    }

    private static (DashboardConfig Config, string RawFence)? ExtractDashboardConfig(string content)
    {
        var matches = DashboardJsonFenceRegex.Matches(content);
        foreach (Match m in matches)
        {
            var inner = m.Groups[1].Value.Trim();
            if (TryParseDashboard(inner, out var config))
                return (config!, m.Value);
        }
        return null;
    }

    private static bool TryParseDashboard(string raw, out DashboardConfig? config)
    {
        config = null;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            if (!doc.RootElement.TryGetProperty("title", out var titleEl)) return false;
            if (!doc.RootElement.TryGetProperty("sections", out var sectionsEl)) return false;
            if (sectionsEl.ValueKind != JsonValueKind.Array) return false;

            config = JsonSerializer.Deserialize<DashboardConfig>(raw, JsonOpts);
            return config is not null && config.Sections is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static (IReadOnlyList<DashboardKpiBlock> Kpis, IReadOnlyList<DashboardTableBlock> Tables, IReadOnlyList<DashboardChartBlock> Charts) ExtractDashboardBlocks(
        DashboardConfig? config,
        CultureInfo culture)
    {
        if (config is null || config.Sections is null)
            return (Array.Empty<DashboardKpiBlock>(), Array.Empty<DashboardTableBlock>(), Array.Empty<DashboardChartBlock>());

        var kpis = new List<DashboardKpiBlock>();
        var tables = new List<DashboardTableBlock>();
        var charts = new List<DashboardChartBlock>();

        foreach (var section in config.Sections)
        {
            if (section is null) continue;
            var type = section.Type?.Trim().ToLowerInvariant();

            switch (type)
            {
                case "kpi_card":
                    kpis.Add(MapKpi(section, culture));
                    break;

                case "table":
                    tables.Add(MapTable(section));
                    break;

                case "chart":
                    var chart = MapChart(section);
                    if (chart is not null) charts.Add(chart);
                    break;
            }
        }

        return (kpis, tables, charts);
    }

    private static DashboardKpiBlock MapKpi(DashboardSection section, CultureInfo culture)
    {
        var dataElement = section.Data;
        string? label = section.Title;
        string? rawValue = null;
        string? trend = null;
        string? unit = null;

        if (dataElement is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("value", out var valueEl))
                rawValue = FormatJsonValue(valueEl, culture);
            if (el.TryGetProperty("unit", out var unitEl) && unitEl.ValueKind == JsonValueKind.String)
                unit = unitEl.GetString();
            if (el.TryGetProperty("trend", out var trendEl))
                trend = FormatTrend(trendEl, culture);
        }

        return new DashboardKpiBlock(label, rawValue, trend, unit);
    }

    private static DashboardTableBlock MapTable(DashboardSection section)
    {
        var columns = new List<DashboardTableColumn>();
        var rows = new List<IReadOnlyList<string>>();

        if (section.Data is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("columns", out var colsEl) && colsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in colsEl.EnumerateArray())
                {
                    if (c.ValueKind != JsonValueKind.Object) continue;
                    var key = c.TryGetProperty("key", out var keyEl) && keyEl.ValueKind == JsonValueKind.String
                        ? keyEl.GetString() ?? string.Empty
                        : string.Empty;
                    var label = c.TryGetProperty("label", out var labelEl) && labelEl.ValueKind == JsonValueKind.String
                        ? labelEl.GetString() ?? key
                        : key;
                    columns.Add(new DashboardTableColumn(key, label));
                }
            }

            if (el.TryGetProperty("rows", out var rowsEl) && rowsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in rowsEl.EnumerateArray())
                {
                    if (r.ValueKind != JsonValueKind.Object) continue;
                    var values = new List<string>(columns.Count);
                    foreach (var col in columns)
                    {
                        if (r.TryGetProperty(col.Key, out var cellEl))
                        {
                            values.Add(FormatJsonValue(cellEl, CultureInfo.InvariantCulture) ?? string.Empty);
                        }
                        else
                        {
                            values.Add(string.Empty);
                        }
                    }
                    rows.Add(values);
                }
            }
        }

        return new DashboardTableBlock(section.Title, columns, rows);
    }

    private static DashboardChartBlock? MapChart(DashboardSection section)
    {
        if (section.Data is not JsonElement data || data.ValueKind != JsonValueKind.Object)
            return null;

        var chartType = data.TryGetProperty("chartType", out var ctEl) && ctEl.ValueKind == JsonValueKind.String
            ? ctEl.GetString() ?? "bar"
            : "bar";

        var labels = new List<string>();
        if (data.TryGetProperty("labels", out var labelsEl) && labelsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var l in labelsEl.EnumerateArray())
            {
                labels.Add(l.ValueKind == JsonValueKind.String ? l.GetString() ?? string.Empty : l.ToString() ?? string.Empty);
            }
        }

        var series = new List<DashboardChartSeries>();
        if (data.TryGetProperty("datasets", out var datasetsEl) && datasetsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var ds in datasetsEl.EnumerateArray())
            {
                if (ds.ValueKind != JsonValueKind.Object) continue;
                var label = ds.TryGetProperty("label", out var lblEl) && lblEl.ValueKind == JsonValueKind.String
                    ? lblEl.GetString() ?? "Série"
                    : "Série";
                var values = new List<double?>();
                if (ds.TryGetProperty("data", out var valsEl) && valsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in valsEl.EnumerateArray())
                    {
                        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d))
                            values.Add(d);
                        else
                            values.Add(null);
                    }
                }
                series.Add(new DashboardChartSeries(label, values));
            }
        }

        return new DashboardChartBlock(section.Title, chartType, labels, series);
    }

    private static string? FormatJsonValue(JsonElement el, CultureInfo culture)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetDouble(out var d) ? d.ToString("N2", culture) : el.GetRawText(),
            JsonValueKind.True => "Oui",
            JsonValueKind.False => "Non",
            JsonValueKind.Null => null,
            _ => el.GetRawText()
        };
    }

    private static string? FormatTrend(JsonElement el, CultureInfo culture)
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var d))
        {
            var sign = d >= 0 ? "+" : "";
            return $"{sign}{d.ToString("N1", culture)} %";
        }
        return FormatJsonValue(el, culture);
    }

    private static IReadOnlyList<string> ExtractSourceTools(Conversation conversation, ConversationMessage assistantMessage)
    {
        var sources = new List<string>();
        var sortOrder = assistantMessage.SortOrder;

        // Look back for tool result messages and assistant tool-call entries that immediately
        // precede the current assistant message.
        foreach (var msg in conversation.Messages.OrderBy(m => m.SortOrder))
        {
            if (msg.SortOrder >= sortOrder) break;
            if (msg.Role == MessageRole.Tool && !string.IsNullOrWhiteSpace(msg.ToolName))
                sources.Add(msg.ToolName!);
        }

        return sources.Distinct().ToList();
    }

    private static string? SanitizeTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        if (trimmed.Length > 140)
            trimmed = trimmed[..140].TrimEnd() + "…";
        return trimmed;
    }

    private static string? DeriveTitleFromMarkdown(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return null;

        var firstHeading = Regex.Match(markdown, @"^\s*#{1,3}\s+(.+)$", RegexOptions.Multiline);
        if (firstHeading.Success)
            return SanitizeTitle(firstHeading.Groups[1].Value);

        var firstNonEmpty = markdown
            .Split('\n', StringSplitOptions.None)
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrEmpty(line));

        return SanitizeTitle(firstNonEmpty);
    }

    private sealed class DashboardConfig
    {
        public string? Title { get; set; }
        public List<DashboardSection>? Sections { get; set; }
    }

    private sealed class DashboardSection
    {
        public string? Type { get; set; }
        public string? Title { get; set; }
        public JsonElement? Data { get; set; }
    }
}
