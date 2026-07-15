using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;

/// <summary>
/// Promotes markdown tables under « Indicateurs clés » (or KPI-shaped tables) into
/// <see cref="DashboardKpiBlock"/> instances for the KPI slide builder.
/// </summary>
internal static class MarkdownKpiHeuristics
{
    private static readonly string[] KpiSectionKeywords =
    [
        "indicateurs clés",
        "indicateurs cles",
        "key indicators",
        "kpis"
    ];

    public static (string RemainingMarkdown, IReadOnlyList<DashboardKpiBlock> Kpis) TryExtract(
        string markdown,
        IReadOnlyList<MarkdownTableExtractor.ExtractedMarkdownTable> tables)
    {
        if (tables.Count == 0)
            return (markdown, Array.Empty<DashboardKpiBlock>());

        var kpis = new List<DashboardKpiBlock>();
        var tablesToRemove = new List<MarkdownTableExtractor.ExtractedMarkdownTable>();

        foreach (var table in tables)
        {
            if (!IsKpiTable(table))
                continue;

            foreach (var row in table.Rows)
            {
                if (row.Count < 2) continue;
                var label = row[0];
                var value = row[1];
                var unit = DetectUnit(row, table.Headers);
                kpis.Add(new DashboardKpiBlock(label, value, Trend: null, Unit: unit));
            }

            tablesToRemove.Add(table);
        }

        if (kpis.Count == 0)
            return (markdown, Array.Empty<DashboardKpiBlock>());

        // Re-run extraction excluding KPI tables
        var remaining = markdown;
        foreach (var table in tablesToRemove)
        {
            remaining = RemoveTableBlock(remaining, table);
        }

        return (remaining.Trim(), kpis);
    }

    private static bool IsKpiTable(MarkdownTableExtractor.ExtractedMarkdownTable table)
    {
        if (table.Headers.Count is < 2 or > 4)
            return false;

        if (table.Rows.Count is < 1 or > 8)
            return false;

        var section = table.PrecedingSectionTitle?.Trim().ToLowerInvariant() ?? string.Empty;
        if (KpiSectionKeywords.Any(k => section.Contains(k, StringComparison.Ordinal)))
            return true;

        // Heuristic: 2-3 columns with numeric second column
        if (table.Headers.Count <= 3 &&
            table.Rows.All(r => r.Count >= 2 && LooksNumeric(r[1])))
            return true;

        return false;
    }

    private static bool LooksNumeric(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var cleaned = value
            .Replace("TND", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty)
            .Replace("\u00A0", string.Empty)
            .Trim();
        return cleaned.Length > 0 && cleaned.All(c => char.IsDigit(c) || c is '.' or ',' or '-' or '+');
    }

    private static string? DetectUnit(IReadOnlyList<string> row, IReadOnlyList<string> headers)
    {
        var headerText = string.Join(' ', headers).ToUpperInvariant();
        if (headerText.Contains("TND", StringComparison.Ordinal))
            return "TND";

        var value = row.Count > 1 ? row[1] : string.Empty;
        if (value.Contains("TND", StringComparison.OrdinalIgnoreCase))
            return "TND";

        return null;
    }

    private static string RemoveTableBlock(string markdown, MarkdownTableExtractor.ExtractedMarkdownTable table)
    {
        var (_, allTables) = MarkdownTableExtractor.Extract(markdown);
        var target = allTables.FirstOrDefault(t =>
            t.Headers.SequenceEqual(table.Headers) && t.Rows.Count == table.Rows.Count);
        if (target is null)
            return markdown;

        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        var start = FindTableStart(lines, target.Headers);
        if (start < 0) return markdown;

        var end = start + 1; // separator
        while (end + 1 < lines.Count && lines[end + 1].Trim().Contains('|'))
            end++;

        lines.RemoveRange(start, end - start + 1);
        return string.Join('\n', lines);
    }

    private static int FindTableStart(List<string> lines, IReadOnlyList<string> headers)
    {
        for (var i = 0; i < lines.Count - 1; i++)
        {
            var trimmed = lines[i].Trim();
            if (!trimmed.Contains('|')) continue;
            var cells = trimmed.Trim('|').Split('|').Select(c => c.Trim()).ToList();
            if (cells.Count >= 2 && cells[0].Equals(headers[0], StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
}
