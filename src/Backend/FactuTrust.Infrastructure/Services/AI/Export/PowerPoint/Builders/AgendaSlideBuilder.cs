using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using A = DocumentFormat.OpenXml.Drawing;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

/// <summary>
/// Renders the agenda slide — a numbered bullet list of the response titles included in the deck.
/// The agenda always appears right after the cover and gives the audience an outline of the
/// presentation before diving into content.
/// </summary>
public sealed class AgendaSlideBuilder : ISlideBuilder
{
    private readonly IReadOnlyList<AssistantResponseSlideModel> _responses;

    public AgendaSlideBuilder(IReadOnlyList<AssistantResponseSlideModel> responses)
    {
        _responses = responses;
    }

    public int Build(SlideBuildContext context)
    {
        if (_responses.Count == 0)
            return 0;

        var (_, tree, _) = SlideFactory.CreateSlide(context);

        SlideHeaderFooterDecorator.AppendHeader(context, tree, "Agenda");

        var theme = context.Theme;
        var dims = context.Dimensions;

        var paragraphs = new List<A.Paragraph>();
        var index = 1;

        foreach (var response in _responses.Take(20))
        {
            paragraphs.Add(BuildAgendaItem(theme, index, response.Title));
            paragraphs.Add(OpenXmlPresentationHelpers.EmptyParagraph());
            index++;
        }

        if (_responses.Count > 20)
        {
            paragraphs.Add(OpenXmlPresentationHelpers.Paragraph(
                text: $"… et {_responses.Count - 20} autres réponses",
                runProperties: OpenXmlPresentationHelpers.RunProperties(
                    fontSizeHundredths: 1600,
                    colorHex: theme.Colors.MutedHex,
                    italic: true,
                    fontFamily: theme.Fonts.BodyFamily),
                paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left)));
        }

        var bodyShape = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "AgendaBody",
            offsetX: dims.BodyLeft,
            offsetY: dims.BodyTop,
            width: dims.ContentWidth,
            height: dims.BodyHeight,
            fill: null,
            paragraphs: paragraphs);
        tree.Append(bodyShape);

        return 1;
    }

    private static A.Paragraph BuildAgendaItem(Themes.IPowerPointTheme theme, int index, string title)
    {
        var paragraph = new A.Paragraph();
        paragraph.Append(OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left));

        paragraph.Append(OpenXmlPresentationHelpers.TextRun(
            $"{index:D2}.  ",
            OpenXmlPresentationHelpers.RunProperties(
                fontSizeHundredths: 2000,
                colorHex: theme.Colors.AccentHex,
                bold: true,
                fontFamily: theme.Fonts.TitleFamily)));

        paragraph.Append(OpenXmlPresentationHelpers.TextRun(
            title,
            OpenXmlPresentationHelpers.RunProperties(
                fontSizeHundredths: 2000,
                colorHex: theme.Colors.OnSurfaceHex,
                fontFamily: theme.Fonts.BodyFamily)));

        return paragraph;
    }
}
