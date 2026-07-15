using System.Globalization;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;

/// <summary>
/// Post-parses assistant markdown to extract KPIs, tables and semantic sections when dashboard
/// JSON is missing or incomplete.
/// </summary>
internal static class AssistantResponseEnricher
{
    public static AssistantResponseSlideModel Enrich(
        AssistantResponseSlideModel model,
        PowerPointRenderingOptions options)
    {
        if (!options.EnhancedRenderingEnabled)
            return model;

        var markdown = model.MarkdownText ?? string.Empty;
        var kpis = model.Kpis.ToList();
        var tables = model.Tables.ToList();

        if (options.MarkdownTableExtractionEnabled)
        {
            var (afterTables, extractedTables) = MarkdownTableExtractor.Extract(markdown);

            if (kpis.Count == 0)
            {
                var (afterKpis, extractedKpis) = MarkdownKpiHeuristics.TryExtract(afterTables, extractedTables);
                afterTables = afterKpis;
                kpis.AddRange(extractedKpis);
            }

            if (tables.Count == 0)
            {
                foreach (var table in extractedTables)
                {
                    if (kpis.Count > 0 &&
                        table.PrecedingSectionTitle?.Contains("Indicateurs", StringComparison.OrdinalIgnoreCase) == true)
                        continue;

                    tables.Add(ToDashboardTable(table));
                }

                if (tables.Count > 0)
                    afterTables = RemoveExtractedTablesFromMarkdown(afterTables, extractedTables, kpis.Count > 0);
            }

            markdown = afterTables;
        }

        IReadOnlyList<MarkdownSectionModel> sections = Array.Empty<MarkdownSectionModel>();
        if (options.SemanticSectionSlidesEnabled && !string.IsNullOrWhiteSpace(markdown))
        {
            sections = MarkdownSectionSegmenter.Segment(markdown)
                .Where(s => s.SectionType != MarkdownSectionSegmenter.SectionType.KeyIndicators)
                .Where(s => !string.IsNullOrWhiteSpace(s.Content))
                .Select(s => new MarkdownSectionModel(
                    s.Title,
                    s.Content,
                    MapKind(s.SectionType)))
                .ToList();

            if (sections.Count > 0)
                markdown = null;
        }

        return model with
        {
            MarkdownText = markdown,
            Kpis = kpis,
            Tables = tables,
            Sections = sections
        };
    }

    private static string RemoveExtractedTablesFromMarkdown(
        string markdown,
        IReadOnlyList<MarkdownTableExtractor.ExtractedMarkdownTable> extractedTables,
        bool kpisExtracted)
    {
        var result = markdown;
        foreach (var table in extractedTables)
        {
            if (kpisExtracted &&
                table.PrecedingSectionTitle?.Contains("Indicateurs", StringComparison.OrdinalIgnoreCase) == true)
                continue;

            var (_, remaining) = MarkdownTableExtractor.Extract(result);
            if (remaining.Count == 0) break;
            result = StripFirstMatchingTable(result, table);
        }
        return result.Trim();
    }

    private static string StripFirstMatchingTable(
        string markdown,
        MarkdownTableExtractor.ExtractedMarkdownTable target)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        for (var i = 0; i < lines.Count - 1; i++)
        {
            if (!lines[i].Trim().Contains('|')) continue;
            if (i + 1 >= lines.Count) break;
            var headerCells = lines[i].Trim().Trim('|').Split('|').Select(c => c.Trim()).ToList();
            if (headerCells.Count == 0 || !headerCells[0].Equals(target.Headers[0], StringComparison.OrdinalIgnoreCase))
                continue;

            var end = i + 1;
            while (end + 1 < lines.Count && lines[end + 1].Trim().Contains('|'))
                end++;

            lines.RemoveRange(i, end - i + 1);
            return string.Join('\n', lines);
        }
        return markdown;
    }

    private static DashboardTableBlock ToDashboardTable(MarkdownTableExtractor.ExtractedMarkdownTable table)
    {
        var columns = table.Headers
            .Select((h, idx) => new DashboardTableColumn($"col{idx}", h))
            .ToList();

        var rows = table.Rows
            .Select(r =>
            {
                var cells = new List<string>(columns.Count);
                for (var i = 0; i < columns.Count; i++)
                    cells.Add(i < r.Count ? r[i] : string.Empty);
                return (IReadOnlyList<string>)cells;
            })
            .ToList();

        return new DashboardTableBlock(
            Title: table.PrecedingSectionTitle ?? "Tableau",
            Columns: columns,
            Rows: rows);
    }

    private static MarkdownSectionKind MapKind(MarkdownSectionSegmenter.SectionType type) =>
        type switch
        {
            MarkdownSectionSegmenter.SectionType.ExecutiveSummary => MarkdownSectionKind.ExecutiveSummary,
            MarkdownSectionSegmenter.SectionType.KeyIndicators => MarkdownSectionKind.KeyIndicators,
            MarkdownSectionSegmenter.SectionType.DetailedAnalysis => MarkdownSectionKind.DetailedAnalysis,
            MarkdownSectionSegmenter.SectionType.Risks => MarkdownSectionKind.Risks,
            MarkdownSectionSegmenter.SectionType.Opportunities => MarkdownSectionKind.Opportunities,
            MarkdownSectionSegmenter.SectionType.Actions => MarkdownSectionKind.Actions,
            MarkdownSectionSegmenter.SectionType.DataLimits => MarkdownSectionKind.DataLimits,
            _ => MarkdownSectionKind.Generic
        };
}
