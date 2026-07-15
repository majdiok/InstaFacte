using System.Text;
using System.Text.RegularExpressions;

namespace FactuTrust.Application.Features.Channels;

/// <summary>
/// Dégradation du Markdown de l'assistant vers le formatage WhatsApp :
/// <list type="bullet">
///   <item><c>**gras**</c> / <c>__gras__</c> → <c>*gras*</c> (gras WhatsApp) ;</item>
///   <item>titres <c># …</c> → ligne en gras ;</item>
///   <item>tableaux Markdown → bloc monospace <c>```…```</c> (rendu aligné par WhatsApp) ;</item>
///   <item><c>[texte](url)</c> → <c>texte (url)</c> ; règles horizontales et citations retirées ;</item>
///   <item>découpage en morceaux ≤ maxChars sur des frontières de paragraphes, suffixe (i/n).</item>
/// </list>
/// Statique et sans dépendance : testable unitairement.
/// </summary>
public static partial class WhatsAppTextFormatter
{
    [GeneratedRegex(@"\*\*(?<t>[^*]+)\*\*", RegexOptions.Singleline)]
    private static partial Regex BoldStarsRegex();

    [GeneratedRegex(@"__(?<t>[^_]+)__", RegexOptions.Singleline)]
    private static partial Regex BoldUnderscoresRegex();

    [GeneratedRegex(@"^#{1,6}\s+(?<t>.+?)\s*#*\s*$", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"\[(?<t>[^\]]+)\]\((?<u>[^)\s]+)\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"^\s*([-*_]){3,}\s*$", RegexOptions.Multiline)]
    private static partial Regex HorizontalRuleRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessNewlinesRegex();

    /// <summary>Convertit un contenu Markdown assistant en texte WhatsApp.</summary>
    public static string Format(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var text = markdown.Replace("\r\n", "\n").Trim();

        // Tableaux d'abord (avant que les autres règles ne touchent leurs cellules).
        text = ConvertTables(text);

        text = BoldStarsRegex().Replace(text, "*${t}*");
        text = BoldUnderscoresRegex().Replace(text, "*${t}*");
        text = HeadingRegex().Replace(text, "*${t}*");
        text = LinkRegex().Replace(text, "${t} (${u})");
        text = HorizontalRuleRegex().Replace(text, string.Empty);

        var lines = text.Split('\n');
        var builder = new StringBuilder(text.Length);
        var inCodeFence = false;
        foreach (var rawLine in lines)
        {
            var line = rawLine;
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inCodeFence = !inCodeFence;
                builder.Append(line.TrimEnd()).Append('\n');
                continue;
            }

            if (!inCodeFence)
            {
                // Citations retirées ; puces « * » converties (conflit avec le gras WhatsApp).
                var trimmedStart = line.TrimStart();
                if (trimmedStart.StartsWith("> ", StringComparison.Ordinal))
                    line = line.Replace("> ", string.Empty);
                else if (trimmedStart.StartsWith("* ", StringComparison.Ordinal))
                    line = string.Concat(line.AsSpan(0, line.Length - trimmedStart.Length), "- ", trimmedStart.AsSpan(2));
            }

            builder.Append(line.TrimEnd()).Append('\n');
        }

        text = builder.ToString();
        text = ExcessNewlinesRegex().Replace(text, "\n\n");
        return text.Trim();
    }

    /// <summary>
    /// Découpe un texte en morceaux ≤ <paramref name="maxChars"/> sur des frontières de paragraphes
    /// (puis de lignes, puis coupe dure en dernier recours). Suffixe « (i/n) » quand plusieurs.
    /// </summary>
    public static IReadOnlyList<string> Split(string? text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        if (maxChars < 32)
            maxChars = 32;

        var trimmed = text.Trim();
        if (trimmed.Length <= maxChars)
            return new[] { trimmed };

        // Réserve pour le suffixe « (i/n) » ajouté après coup.
        var budget = maxChars - 10;
        var chunks = new List<string>();
        var current = new StringBuilder(budget);

        foreach (var paragraph in trimmed.Split("\n\n"))
        {
            var pieces = paragraph.Length <= budget
                ? new[] { paragraph }
                : SplitOversized(paragraph, budget);

            foreach (var piece in pieces)
            {
                var separatorLength = current.Length == 0 ? 0 : 2;
                if (current.Length + separatorLength + piece.Length > budget && current.Length > 0)
                {
                    chunks.Add(current.ToString());
                    current.Clear();
                }

                if (current.Length > 0)
                    current.Append("\n\n");
                current.Append(piece);
            }
        }

        if (current.Length > 0)
            chunks.Add(current.ToString());

        if (chunks.Count <= 1)
            return chunks;

        return chunks
            .Select((chunk, index) => $"{chunk}\n\n({index + 1}/{chunks.Count})")
            .ToList();
    }

    private static IEnumerable<string> SplitOversized(string paragraph, int budget)
    {
        // Paragraphe trop long : coupe par lignes, puis coupe dure si une ligne dépasse encore.
        var current = new StringBuilder(budget);
        foreach (var line in paragraph.Split('\n'))
        {
            var remaining = line;
            while (remaining.Length > budget)
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                yield return remaining[..budget];
                remaining = remaining[budget..];
            }

            var separatorLength = current.Length == 0 ? 0 : 1;
            if (current.Length + separatorLength + remaining.Length > budget && current.Length > 0)
            {
                yield return current.ToString();
                current.Clear();
            }

            if (current.Length > 0)
                current.Append('\n');
            current.Append(remaining);
        }

        if (current.Length > 0)
            yield return current.ToString();
    }

    private static string ConvertTables(string text)
    {
        var lines = text.Split('\n');
        var builder = new StringBuilder(text.Length);
        var tableLines = new List<string>();

        void FlushTable()
        {
            if (tableLines.Count == 0)
                return;

            builder.Append("```\n");
            foreach (var row in RenderTable(tableLines))
                builder.Append(row).Append('\n');
            builder.Append("```\n");
            tableLines.Clear();
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var isTableRow = trimmed.StartsWith('|') && trimmed.EndsWith('|') && trimmed.Length > 1;
            if (isTableRow)
            {
                tableLines.Add(trimmed);
                continue;
            }

            FlushTable();
            builder.Append(line).Append('\n');
        }

        FlushTable();
        return builder.ToString().TrimEnd('\n');
    }

    private static IEnumerable<string> RenderTable(IReadOnlyList<string> tableLines)
    {
        var rows = new List<string[]>();
        foreach (var line in tableLines)
        {
            var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            // Ligne séparatrice « |---|---| » ignorée.
            if (cells.All(c => c.Length > 0 && c.All(ch => ch is '-' or ':' or ' ')))
                continue;
            rows.Add(cells);
        }

        if (rows.Count == 0)
            yield break;

        var columnCount = rows.Max(r => r.Length);
        var widths = new int[columnCount];
        foreach (var row in rows)
        {
            for (var i = 0; i < row.Length; i++)
                widths[i] = Math.Max(widths[i], row[i].Length);
        }

        foreach (var row in rows)
        {
            var rendered = new StringBuilder();
            for (var i = 0; i < row.Length; i++)
            {
                if (i > 0)
                    rendered.Append("  ");
                rendered.Append(row[i].PadRight(widths[i]));
            }

            yield return rendered.ToString().TrimEnd();
        }
    }
}
