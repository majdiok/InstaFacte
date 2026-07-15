namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;

/// <summary>
/// Splits assistant markdown into semantic sections based on <c>##</c> headings.
/// </summary>
internal static class MarkdownSectionSegmenter
{
    public enum SectionType
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

    public sealed record MarkdownSectionBlock(
        string Title,
        string Content,
        SectionType SectionType);

    public static IReadOnlyList<MarkdownSectionBlock> Segment(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return Array.Empty<MarkdownSectionBlock>();

        var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        var sections = new List<MarkdownSectionBlock>();
        string? currentTitle = null;
        var currentLines = new List<string>();

        void Flush()
        {
            if (currentTitle is null && currentLines.Count == 0)
                return;

            var title = currentTitle ?? "Contenu";
            var content = string.Join('\n', currentLines).Trim();
            if (string.IsNullOrWhiteSpace(content) && currentTitle is null)
                return;

            sections.Add(new MarkdownSectionBlock(
                Title: title,
                Content: content,
                SectionType: ClassifySection(title)));
            currentLines.Clear();
        }

        foreach (var line in lines)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Flush();
                currentTitle = line[3..].Trim();
                continue;
            }

            currentLines.Add(line);
        }

        Flush();
        return sections;
    }

    public static SectionType ClassifySection(string title)
    {
        var lower = title.Trim().ToLowerInvariant();
        if (lower.Contains("synthèse exécutive") || lower.Contains("synthese executive"))
            return SectionType.ExecutiveSummary;
        if (lower.Contains("indicateurs clés") || lower.Contains("indicateurs cles"))
            return SectionType.KeyIndicators;
        if (lower.Contains("analyse détaillée") || lower.Contains("analyse detaillee"))
            return SectionType.DetailedAnalysis;
        if (lower.Contains("anomalies") || lower.Contains("risques"))
            return SectionType.Risks;
        if (lower.Contains("opportunit"))
            return SectionType.Opportunities;
        if (lower.Contains("actions recommand"))
            return SectionType.Actions;
        if (lower.Contains("points à vérifier") || lower.Contains("limites"))
            return SectionType.DataLimits;
        return SectionType.Generic;
    }
}
