using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using A = DocumentFormat.OpenXml.Drawing;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

/// <summary>
/// Two-column executive summary slide: narrative left, highlighted key figure right.
/// </summary>
public sealed class ExecutiveSummarySlideBuilder
{
    public int BuildSection(
        SlideBuildContext context,
        AssistantResponseSlideModel response,
        MarkdownSectionModel section)
    {
        var content = SectionSlideHelpers.StripDuplicateHeading(section.Content, section.Title);
        if (string.IsNullOrWhiteSpace(content))
            return 0;

        var notes = $"Section : {section.Title}\nConversation : {response.ConversationId:D}\nMessage : {response.MessageId:D}";
        var (_, tree, _) = SlideFactory.CreateSlide(context, notes);
        SlideHeaderFooterDecorator.AppendHeader(context, tree, section.Title);

        var theme = context.Theme;
        var dims = context.Dimensions;
        var gutter = OpenXmlPresentationHelpers.PxToEmu(24);
        var leftWidth = (dims.ContentWidth - gutter) * 2 / 3;
        var rightWidth = dims.ContentWidth - gutter - leftWidth;
        var calloutAmount = SectionSlideHelpers.ExtractPrimaryTndAmount(content);

        var leftParagraphs = SectionSlideHelpers.ConvertWithAmountHighlight(content, theme).ToList();
        var leftShape = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "ExecSummaryBody",
            offsetX: dims.BodyLeft,
            offsetY: dims.BodyTop,
            width: leftWidth,
            height: dims.BodyHeight,
            fill: null,
            paragraphs: leftParagraphs.Select(p => (A.Paragraph)p.CloneNode(true)).ToList());
        tree.Append(leftShape);

        if (!string.IsNullOrWhiteSpace(calloutAmount))
        {
            var calloutX = dims.BodyLeft + leftWidth + gutter;
            var calloutBg = OpenXmlPresentationHelpers.Shape(
                shapeId: context.NextShapeId(),
                shapeName: "ExecSummaryCallout",
                offsetX: calloutX,
                offsetY: dims.BodyTop,
                width: rightWidth,
                height: OpenXmlPresentationHelpers.PxToEmu(160),
                fill: OpenXmlPresentationHelpers.SolidFill(theme.Colors.AccentSoftHex),
                paragraphs: new[] { OpenXmlPresentationHelpers.EmptyParagraph() },
                leftInsetEmu: OpenXmlPresentationHelpers.PxToEmu(20),
                rightInsetEmu: OpenXmlPresentationHelpers.PxToEmu(20),
                topInsetEmu: OpenXmlPresentationHelpers.PxToEmu(20),
                bottomInsetEmu: OpenXmlPresentationHelpers.PxToEmu(20));
            tree.Append(calloutBg);

            var labelShape = OpenXmlPresentationHelpers.Shape(
                shapeId: context.NextShapeId(),
                shapeName: "ExecSummaryCalloutLabel",
                offsetX: calloutX + OpenXmlPresentationHelpers.PxToEmu(20),
                offsetY: dims.BodyTop + OpenXmlPresentationHelpers.PxToEmu(24),
                width: rightWidth - OpenXmlPresentationHelpers.PxToEmu(40),
                height: OpenXmlPresentationHelpers.PxToEmu(28),
                fill: null,
                paragraphs: new[]
                {
                    OpenXmlPresentationHelpers.Paragraph(
                        text: "Chiffre clé",
                        runProperties: OpenXmlPresentationHelpers.RunProperties(
                            fontSizeHundredths: 1100,
                            colorHex: theme.Colors.MutedHex,
                            fontFamily: theme.Fonts.BodyFamily),
                        paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left))
                });
            tree.Append(labelShape);

            var valueShape = OpenXmlPresentationHelpers.Shape(
                shapeId: context.NextShapeId(),
                shapeName: "ExecSummaryCalloutValue",
                offsetX: calloutX + OpenXmlPresentationHelpers.PxToEmu(20),
                offsetY: dims.BodyTop + OpenXmlPresentationHelpers.PxToEmu(56),
                width: rightWidth - OpenXmlPresentationHelpers.PxToEmu(40),
                height: OpenXmlPresentationHelpers.PxToEmu(80),
                fill: null,
                paragraphs: new[]
                {
                    OpenXmlPresentationHelpers.Paragraph(
                        text: calloutAmount!,
                        runProperties: OpenXmlPresentationHelpers.RunProperties(
                            fontSizeHundredths: 3200,
                            colorHex: theme.Colors.AccentHex,
                            bold: true,
                            fontFamily: theme.Fonts.TitleFamily),
                        paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left))
                });
            tree.Append(valueShape);
        }

        context.AppendContentFooter(tree);
        return 1;
    }
}
