using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using A = DocumentFormat.OpenXml.Drawing;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

/// <summary>
/// Renders a table-of-contents slide with response titles + their estimated slide ranges. Useful
/// for long decks (10+ responses). When the agenda is also enabled, this acts as a finer-grained
/// outline including content type per section.
/// </summary>
public sealed class TocSlideBuilder : ISlideBuilder
{
    private readonly IReadOnlyList<TocEntry> _entries;

    public TocSlideBuilder(IReadOnlyList<TocEntry> entries)
    {
        _entries = entries;
    }

    public int Build(SlideBuildContext context)
    {
        if (_entries.Count == 0)
            return 0;

        var (_, tree, _) = SlideFactory.CreateSlide(context);

        SlideHeaderFooterDecorator.AppendHeader(context, tree, "Sommaire");

        var theme = context.Theme;
        var dims = context.Dimensions;

        var paragraphs = new List<A.Paragraph>();

        foreach (var entry in _entries.Take(20))
        {
            paragraphs.Add(BuildEntry(theme, entry));
            paragraphs.Add(OpenXmlPresentationHelpers.EmptyParagraph());
        }

        var bodyShape = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "TocBody",
            offsetX: dims.BodyLeft,
            offsetY: dims.BodyTop,
            width: dims.ContentWidth,
            height: dims.BodyHeight,
            fill: null,
            paragraphs: paragraphs);
        tree.Append(bodyShape);

        return 1;
    }

    private static A.Paragraph BuildEntry(Themes.IPowerPointTheme theme, TocEntry entry)
    {
        var paragraph = new A.Paragraph();
        paragraph.Append(OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left));

        paragraph.Append(OpenXmlPresentationHelpers.TextRun(
            entry.Title,
            OpenXmlPresentationHelpers.RunProperties(
                fontSizeHundredths: 1600,
                colorHex: theme.Colors.OnSurfaceHex,
                bold: true,
                fontFamily: theme.Fonts.BodyFamily)));

        if (!string.IsNullOrWhiteSpace(entry.Summary))
        {
            paragraph.Append(OpenXmlPresentationHelpers.TextRun(
                $"   —   {entry.Summary}",
                OpenXmlPresentationHelpers.RunProperties(
                    fontSizeHundredths: 1400,
                    colorHex: theme.Colors.MutedHex,
                    fontFamily: theme.Fonts.BodyFamily)));
        }

        paragraph.Append(OpenXmlPresentationHelpers.TextRun(
            $"    p. {entry.PageNumber}",
            OpenXmlPresentationHelpers.RunProperties(
                fontSizeHundredths: 1400,
                colorHex: theme.Colors.AccentHex,
                fontFamily: theme.Fonts.BodyFamily)));

        return paragraph;
    }
}

public sealed record TocEntry(string Title, string? Summary, int PageNumber);
