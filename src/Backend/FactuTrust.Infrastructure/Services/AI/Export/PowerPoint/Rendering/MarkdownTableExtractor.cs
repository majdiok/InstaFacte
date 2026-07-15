using System.Text.RegularExpressions;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;

/// <summary>
/// Detects GitHub-flavoured markdown table blocks and extracts structured rows.
/// </summary>
internal static class MarkdownTableExtractor
{
    private static readonly Regex SeparatorRowRegex = new(
        @"^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)+\|?\s*$",
        RegexOptions.Compiled);

    public sealed record ExtractedMarkdownTable(
        int StartLineIndex,
        int EndLineIndex,
        IReadOnlyList<string> Headers,
        IReadOnlyList<IReadOnlyList<string>> Rows,
        string? PrecedingSectionTitle);

    public static (string RemainingMarkdown, IReadOnlyList<ExtractedMarkdownTable> Tables) Extract(
        string markdown,
        IReadOnlyList<string>? sectionTitles = null)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return (markdown, Array.Empty<ExtractedMarkdownTable>());

        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var tables = new List<ExtractedMarkdownTable>();
        var outputLines = new List<string>(lines.Length);
        var currentSectionTitle = (string?)null;

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                currentSectionTitle = trimmed[3..].Trim();
                outputLines.Add(lines[i]);
                continue;
            }

            if (IsTableHeaderLine(trimmed) &&
                i + 1 < lines.Length &&
                IsSeparatorLine(lines[i + 1].Trim()))
            {
                var headers = ParseCells(trimmed);
                i++; // skip separator
                var rows = new List<IReadOnlyList<string>>();
                while (i + 1 < lines.Length && IsTableRowLine(lines[i + 1].Trim()))
                {
                    i++;
                    rows.Add(ParseCells(lines[i].Trim()));
                }

                if (headers.Count >= 2 && rows.Count > 0)
                {
                    tables.Add(new ExtractedMarkdownTable(
                        StartLineIndex: outputLines.Count,
                        EndLineIndex: outputLines.Count,
                        Headers: headers,
                        Rows: rows,
                        PrecedingSectionTitle: currentSectionTitle));
                    continue;
                }

                // Malformed — keep original lines
                outputLines.Add(lines[i - 1]);
                outputLines.Add(lines[i]);
                for (var r = 0; r < rows.Count; r++)
                    outputLines.Add(lines[i - rows.Count + r]);
                continue;
            }

            outputLines.Add(lines[i]);
        }

        var remaining = string.Join('\n', outputLines);
        remaining = Regex.Replace(remaining, @"\n{3,}", "\n\n").Trim();
        return (remaining, tables);
    }

    private static bool IsTableHeaderLine(string line) =>
        line.Contains('|', StringComparison.Ordinal) &&
        line.Count(c => c == '|') >= 2 &&
        !IsSeparatorLine(line);

    private static bool IsTableRowLine(string line) =>
        line.Contains('|', StringComparison.Ordinal) &&
        line.Count(c => c == '|') >= 2 &&
        !IsSeparatorLine(line);

    private static bool IsSeparatorLine(string line) => SeparatorRowRegex.IsMatch(line);

    private static IReadOnlyList<string> ParseCells(string line)
    {
        var inner = line.Trim().Trim('|');
        return inner
            .Split('|', StringSplitOptions.None)
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .ToList();
    }
}
