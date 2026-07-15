using System.Text;
using System.Text.RegularExpressions;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using A = DocumentFormat.OpenXml.Drawing;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;

/// <summary>
/// Light-weight markdown → OpenXml converter for the subset our LLM emits in assistant responses
/// (headings, bullet/numbered lists, bold, italic, inline code, links). Designed to be safe and
/// dependency-free.
/// </summary>
/// <remarks>
/// Every text fragment goes through <see cref="OpenXmlPresentationHelpers.TextRun"/>, which uses
/// the OpenXml object model and escapes the content automatically. Raw XML never reaches the
/// generated file, eliminating the XML-injection risk of string-based templating.
/// </remarks>
internal static class MarkdownToOpenXmlConverter
{
    private static readonly Regex CodeFenceRegex = new(@"```[\s\S]*?```", RegexOptions.Compiled);
    private static readonly Regex JsonFenceRegex = new(@"```\s*json[\s\S]*?```", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex InlineFormatting = new(
        @"\*\*(?<boldText>[^\*\n]+)\*\*|\*(?<italicText>[^\*\n]+)\*|`(?<codeText>[^`\n]+)`|\[(?<linkLabel>[^\]]+)\]\((?<linkUrl>[^)]+)\)",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses <paramref name="markdown"/> and returns a sequence of paragraphs ready to be added
    /// to a text body. The conversion is best-effort: unrecognised syntax falls back to plain
    /// text, never to raw XML.
    /// </summary>
    public static IReadOnlyList<A.Paragraph> Convert(string markdown, IPowerPointTheme theme, int baseFontSizeHundredths = 1400)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return Array.Empty<A.Paragraph>();

        // Remove JSON fences (they are persistence artefacts) before parsing.
        var clean = JsonFenceRegex.Replace(markdown, string.Empty);

        // Remove any other code fences — we won't render them visually (mono spacing is hard in PPTX).
        clean = CodeFenceRegex.Replace(clean, match => $"\n{match.Value.Trim('`').Trim()}\n");

        var lines = clean
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');

        var paragraphs = new List<A.Paragraph>(capacity: lines.Length);

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                paragraphs.Add(OpenXmlPresentationHelpers.EmptyParagraph());
                continue;
            }

            paragraphs.Add(ConvertLine(line, theme, baseFontSizeHundredths));
        }

        return paragraphs;
    }

    private static A.Paragraph ConvertLine(string line, IPowerPointTheme theme, int baseFontSizeHundredths)
    {
        var (level, isBullet, content) = ParseLeading(line);

        var (fontSize, isBold, alignment) = ResolveStyle(level, baseFontSizeHundredths);

        var paragraph = new A.Paragraph();
        paragraph.Append(OpenXmlPresentationHelpers.ParagraphProperties(alignment, level == 0 ? 0 : Math.Max(0, level - 1), isBullet));

        AppendRichRuns(paragraph, content, theme, fontSize, isBold);
        return paragraph;
    }

    private static (int level, bool isBullet, string content) ParseLeading(string line)
    {
        // Headings: # / ## / ### …
        if (line.StartsWith('#'))
        {
            var i = 0;
            while (i < line.Length && i < 6 && line[i] == '#') i++;
            if (i < line.Length && line[i] == ' ')
            {
                return (i, false, line[(i + 1)..]);
            }
        }

        // Numbered list: "1. Item"
        var numbered = Regex.Match(line, @"^\s*\d+\.\s+");
        if (numbered.Success)
        {
            return (0, true, line[numbered.Length..]);
        }

        // Bullet list: "- Item" or "* Item"
        var bullet = Regex.Match(line, @"^\s*[-\*]\s+");
        if (bullet.Success)
        {
            var leading = bullet.Value.IndexOf('-') >= 0 ? bullet.Value.IndexOf('-') : bullet.Value.IndexOf('*');
            var indent = leading >= 0 ? leading / 2 : 0;
            return (indent, true, line[bullet.Length..]);
        }

        return (0, false, line);
    }

    private static (int fontSize, bool isBold, A.TextAlignmentTypeValues alignment) ResolveStyle(int headingLevel, int baseFontSize)
    {
        return headingLevel switch
        {
            1 => (2400, true, A.TextAlignmentTypeValues.Left),
            2 => (2000, true, A.TextAlignmentTypeValues.Left),
            3 => (1700, true, A.TextAlignmentTypeValues.Left),
            >= 4 => (1500, true, A.TextAlignmentTypeValues.Left),
            _ => (baseFontSize, false, A.TextAlignmentTypeValues.Left)
        };
    }

    private static void AppendRichRuns(A.Paragraph paragraph, string text, IPowerPointTheme theme, int baseFontSize, bool isBold)
    {
        var lastIndex = 0;
        var matches = InlineFormatting.Matches(text);

        foreach (Match match in matches)
        {
            if (match.Index > lastIndex)
            {
                AppendPlainRun(paragraph, text[lastIndex..match.Index], theme, baseFontSize, isBold);
            }

            if (match.Groups["boldText"].Success)
            {
                AppendStyledRun(paragraph, match.Groups["boldText"].Value, theme, baseFontSize, bold: true, italic: false);
            }
            else if (match.Groups["italicText"].Success)
            {
                AppendStyledRun(paragraph, match.Groups["italicText"].Value, theme, baseFontSize, bold: isBold, italic: true);
            }
            else if (match.Groups["codeText"].Success)
            {
                AppendCodeRun(paragraph, match.Groups["codeText"].Value, theme, baseFontSize);
            }
            else if (match.Groups["linkLabel"].Success)
            {
                // Display link label only — embedding hyperlinks in shape runs requires more plumbing.
                AppendStyledRun(paragraph, match.Groups["linkLabel"].Value, theme, baseFontSize, bold: isBold, italic: false, accent: true);
            }

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
        {
            AppendPlainRun(paragraph, text[lastIndex..], theme, baseFontSize, isBold);
        }
    }

    private static void AppendPlainRun(A.Paragraph paragraph, string text, IPowerPointTheme theme, int baseFontSize, bool isBold)
    {
        if (string.IsNullOrEmpty(text)) return;
        paragraph.Append(OpenXmlPresentationHelpers.TextRun(
            DecodeBasic(text),
            OpenXmlPresentationHelpers.RunProperties(
                fontSizeHundredths: baseFontSize,
                colorHex: theme.Colors.OnSurfaceHex,
                bold: isBold,
                fontFamily: theme.Fonts.BodyFamily)));
    }

    private static void AppendStyledRun(A.Paragraph paragraph, string text, IPowerPointTheme theme, int baseFontSize, bool bold, bool italic, bool accent = false)
    {
        paragraph.Append(OpenXmlPresentationHelpers.TextRun(
            DecodeBasic(text),
            OpenXmlPresentationHelpers.RunProperties(
                fontSizeHundredths: baseFontSize,
                colorHex: accent ? theme.Colors.AccentHex : theme.Colors.OnSurfaceHex,
                bold: bold,
                italic: italic,
                fontFamily: theme.Fonts.BodyFamily)));
    }

    private static void AppendCodeRun(A.Paragraph paragraph, string text, IPowerPointTheme theme, int baseFontSize)
    {
        paragraph.Append(OpenXmlPresentationHelpers.TextRun(
            DecodeBasic(text),
            OpenXmlPresentationHelpers.RunProperties(
                fontSizeHundredths: Math.Max(1000, baseFontSize - 200),
                colorHex: theme.Colors.AccentHex,
                bold: false,
                fontFamily: theme.Fonts.MonoFamily)));
    }

    private static string DecodeBasic(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sb.Append(c switch
            {
                '\u00A0' => ' ',
                _ => c
            });
        }
        return sb.ToString();
    }
}
