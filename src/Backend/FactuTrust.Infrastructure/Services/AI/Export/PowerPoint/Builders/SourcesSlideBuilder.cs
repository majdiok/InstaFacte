using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using A = DocumentFormat.OpenXml.Drawing;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

/// <summary>
/// Renders a "Sources" slide listing the tools / data sources the assistant called to produce
/// each response. Provides traceability and helps stakeholders trust the figures presented.
/// </summary>
public sealed class SourcesSlideBuilder : ISlideBuilder
{
    private readonly IReadOnlyList<AssistantResponseSlideModel> _responses;

    public SourcesSlideBuilder(IReadOnlyList<AssistantResponseSlideModel> responses)
    {
        _responses = responses;
    }

    public int Build(SlideBuildContext context)
    {
        var anySources = _responses.Any(r => r.SourceTools.Count > 0);
        if (!anySources)
            return 0;

        var (_, tree, _) = SlideFactory.CreateSlide(context);

        SlideHeaderFooterDecorator.AppendHeader(context, tree, "Sources et outils");

        var theme = context.Theme;
        var dims = context.Dimensions;

        var paragraphs = new List<A.Paragraph>();
        var index = 1;

        foreach (var response in _responses)
        {
            if (response.SourceTools.Count == 0)
                continue;

            paragraphs.Add(OpenXmlPresentationHelpers.Paragraph(
                text: $"{index:D2}.  {response.Title}",
                runProperties: OpenXmlPresentationHelpers.RunProperties(
                    fontSizeHundredths: 1400,
                    colorHex: theme.Colors.OnSurfaceHex,
                    bold: true,
                    fontFamily: theme.Fonts.BodyFamily),
                paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left)));

            var toolList = string.Join("  •  ", response.SourceTools.Distinct());
            paragraphs.Add(OpenXmlPresentationHelpers.Paragraph(
                text: $"    Outils utilisés : {toolList}",
                runProperties: OpenXmlPresentationHelpers.RunProperties(
                    fontSizeHundredths: 1100,
                    colorHex: theme.Colors.MutedHex,
                    fontFamily: theme.Fonts.BodyFamily),
                paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left)));

            paragraphs.Add(OpenXmlPresentationHelpers.EmptyParagraph());
            index++;
        }

        var bodyShape = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "SourcesBody",
            offsetX: dims.BodyLeft,
            offsetY: dims.BodyTop,
            width: dims.ContentWidth,
            height: dims.BodyHeight,
            fill: null,
            paragraphs: paragraphs);
        tree.Append(bodyShape);

        return 1;
    }
}
