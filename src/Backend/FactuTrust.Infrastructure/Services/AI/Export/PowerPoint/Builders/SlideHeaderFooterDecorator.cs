using DocumentFormat.OpenXml;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

/// <summary>
/// Shared helper that decorates non-cover slides with a consistent header (slide title + accent
/// bar) and footer (deck title, page number, generation date). Keeps every content slide visually
/// coherent across all builders.
/// </summary>
internal static class SlideHeaderFooterDecorator
{
    /// <summary>Appends a header strip and slide title to the supplied shape tree.</summary>
    public static void AppendHeader(SlideBuildContext context, P.ShapeTree tree, string title)
    {
        var theme = context.Theme;
        var dims = context.Dimensions;

        // Accent bar above the title — subtle brand cue.
        var accentBar = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "HeaderAccentBar",
            offsetX: dims.MarginX,
            offsetY: dims.MarginY,
            width: OpenXmlPresentationHelpers.PxToEmu(48),
            height: OpenXmlPresentationHelpers.PxToEmu(4),
            fill: OpenXmlPresentationHelpers.SolidFill(theme.Colors.AccentHex),
            paragraphs: new[] { OpenXmlPresentationHelpers.EmptyParagraph() });
        tree.Append(accentBar);

        // Title text.
        var titleShape = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "HeaderTitle",
            offsetX: dims.MarginX,
            offsetY: dims.MarginY + OpenXmlPresentationHelpers.PxToEmu(10),
            width: dims.ContentWidth,
            height: dims.HeaderHeight,
            fill: null,
            paragraphs: new[]
            {
                OpenXmlPresentationHelpers.Paragraph(
                    text: TruncateTitle(title),
                    runProperties: OpenXmlPresentationHelpers.RunProperties(
                        fontSizeHundredths: 2400,
                        colorHex: theme.Colors.PrimaryHex,
                        bold: true,
                        fontFamily: theme.Fonts.TitleFamily),
                    paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left))
            });
        tree.Append(titleShape);
    }

    /// <summary>Appends a footer strip with deck title (left) and page number / date (right).</summary>
    public static void AppendFooter(SlideBuildContext context, P.ShapeTree tree, int pageNumber, int totalPages)
    {
        var theme = context.Theme;
        var dims = context.Dimensions;
        var dateText = DateTime.Now.ToString("dd MMMM yyyy", context.Culture);

        var leftWidth = dims.ContentWidth / 2;
        var leftFooter = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "FooterLeft",
            offsetX: dims.MarginX,
            offsetY: dims.Height - dims.MarginY,
            width: leftWidth,
            height: dims.FooterHeight,
            fill: null,
            paragraphs: new[]
            {
                OpenXmlPresentationHelpers.Paragraph(
                    text: TruncateFooter(context.Request.Title),
                    runProperties: OpenXmlPresentationHelpers.RunProperties(
                        fontSizeHundredths: 900,
                        colorHex: theme.Colors.MutedHex,
                        fontFamily: theme.Fonts.BodyFamily),
                    paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left))
            });
        tree.Append(leftFooter);

        var rightFooter = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "FooterRight",
            offsetX: dims.MarginX + leftWidth,
            offsetY: dims.Height - dims.MarginY,
            width: leftWidth,
            height: dims.FooterHeight,
            fill: null,
            paragraphs: new[]
            {
                OpenXmlPresentationHelpers.Paragraph(
                    text: $"{dateText}    {pageNumber}/{totalPages}",
                    runProperties: OpenXmlPresentationHelpers.RunProperties(
                        fontSizeHundredths: 900,
                        colorHex: theme.Colors.MutedHex,
                        fontFamily: theme.Fonts.BodyFamily),
                    paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Right))
            });
        tree.Append(rightFooter);
    }

    private static string TruncateTitle(string text)
    {
        const int max = 120;
        if (text.Length <= max) return text;
        return text[..max] + "…";
    }

    private static string TruncateFooter(string text)
    {
        const int max = 90;
        if (text.Length <= max) return text;
        return text[..max] + "…";
    }
}
