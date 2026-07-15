using System.Text.RegularExpressions;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using A = DocumentFormat.OpenXml.Drawing;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

/// <summary>
/// Shared helpers for section-based slide builders (title dedup, footers, amount highlighting).
/// </summary>
internal static class SectionSlideHelpers
{
    private static readonly Regex TndAmountRegex = new(
        @"(\d[\d\s.,]*)\s*(TND)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string StripDuplicateHeading(string content, string slideTitle)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        var normalizedTitle = NormalizeTitle(slideTitle);
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        if (lines.Count == 0) return content;

        var first = lines[0].Trim();
        if (first.StartsWith("## ", StringComparison.Ordinal))
        {
            var heading = first[3..].Trim();
            if (NormalizeTitle(heading) == normalizedTitle)
            {
                lines.RemoveAt(0);
                while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
                    lines.RemoveAt(0);
            }
        }

        return string.Join('\n', lines).Trim();
    }

    public static IReadOnlyList<A.Paragraph> ConvertWithAmountHighlight(
        string markdown,
        IPowerPointTheme theme,
        int baseFontSize = 1400)
    {
        var paragraphs = MarkdownToOpenXmlConverter.Convert(markdown, theme, baseFontSize).ToList();
        foreach (var paragraph in paragraphs)
        {
            HighlightTndAmounts(paragraph, theme, baseFontSize);
        }
        return paragraphs;
    }

    private static void HighlightTndAmounts(A.Paragraph paragraph, IPowerPointTheme theme, int baseFontSize)
    {
        var runs = paragraph.Elements<A.Run>().ToList();
        if (runs.Count == 0) return;

        var fullText = string.Concat(runs.Select(r => r.Text?.Text ?? string.Empty));
        if (!TndAmountRegex.IsMatch(fullText))
            return;

        paragraph.RemoveAllChildren<A.Run>();
        var lastIndex = 0;
        foreach (Match match in TndAmountRegex.Matches(fullText))
        {
            if (match.Index > lastIndex)
            {
                AppendPlainRun(paragraph, fullText[lastIndex..match.Index], theme, baseFontSize);
            }

            var amountText = match.Value.Trim();
            paragraph.Append(OpenXmlPresentationHelpers.TextRun(
                amountText,
                OpenXmlPresentationHelpers.RunProperties(
                    fontSizeHundredths: baseFontSize,
                    colorHex: theme.Colors.AccentHex,
                    bold: true,
                    fontFamily: theme.Fonts.BodyFamily)));
            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < fullText.Length)
            AppendPlainRun(paragraph, fullText[lastIndex..], theme, baseFontSize);
    }

    private static void AppendPlainRun(A.Paragraph paragraph, string text, IPowerPointTheme theme, int baseFontSize)
    {
        if (string.IsNullOrEmpty(text)) return;
        paragraph.Append(OpenXmlPresentationHelpers.TextRun(
            text,
            OpenXmlPresentationHelpers.RunProperties(
                fontSizeHundredths: baseFontSize,
                colorHex: theme.Colors.OnSurfaceHex,
                fontFamily: theme.Fonts.BodyFamily)));
    }

    public static string? ExtractPrimaryTndAmount(string content)
    {
        var match = TndAmountRegex.Match(content);
        if (!match.Success) return null;
        return $"{match.Groups[1].Value.Trim()} {match.Groups[2].Value.ToUpperInvariant()}";
    }

    public static IReadOnlyList<A.Paragraph> ConvertRiskSection(string markdown, IPowerPointTheme theme)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var paragraphs = new List<A.Paragraph>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                paragraphs.Add(OpenXmlPresentationHelpers.EmptyParagraph());
                continue;
            }

            var lower = line.ToLowerInvariant();
            var color = lower.Contains("critique")
                ? theme.Colors.DangerHex
                : lower.Contains("attention")
                    ? theme.Colors.WarningHex
                    : lower.Contains("**ok**") || lower.Contains(" ok")
                        ? theme.Colors.SuccessHex
                        : theme.Colors.OnSurfaceHex;

            paragraphs.Add(OpenXmlPresentationHelpers.Paragraph(
                text: line.Replace("**", string.Empty, StringComparison.Ordinal),
                runProperties: OpenXmlPresentationHelpers.RunProperties(
                    fontSizeHundredths: 1400,
                    colorHex: color,
                    bold: lower.Contains("critique") || lower.Contains("attention"),
                    fontFamily: theme.Fonts.BodyFamily),
                paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left)));
        }

        return paragraphs;
    }

    private static string NormalizeTitle(string title) =>
        title.Trim().ToLowerInvariant()
            .Replace("é", "e", StringComparison.Ordinal)
            .Replace("è", "e", StringComparison.Ordinal);
}
