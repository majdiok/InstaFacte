using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using A = DocumentFormat.OpenXml.Drawing;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

/// <summary>
/// Inserts a visual divider slide before each response group. Gives a clear "chapter break"
/// inside the deck — improves scanability for long presentations.
/// </summary>
public sealed class SectionDividerSlideBuilder : IResponseSlideBuilder
{
    private readonly int _sectionNumber;

    public SectionDividerSlideBuilder(int sectionNumber)
    {
        _sectionNumber = sectionNumber;
    }

    public int Build(SlideBuildContext context, AssistantResponseSlideModel response)
    {
        var (_, tree, _) = SlideFactory.CreateSlide(context);

        var theme = context.Theme;
        var dims = context.Dimensions;

        // Solid block background — accent color soft.
        var background = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "SectionBackground",
            offsetX: 0,
            offsetY: 0,
            width: dims.Width,
            height: dims.Height,
            fill: OpenXmlPresentationHelpers.SolidFill(theme.Colors.SurfaceHex),
            paragraphs: new[] { OpenXmlPresentationHelpers.EmptyParagraph() });
        tree.Append(background);

        // Premium-V2 themes (CoverStyle ≥ 8) get an oversized section number rendered behind the
        // title in `accentSoft` — adds editorial weight without colliding with the foreground text.
        var premium = (int)theme.CoverStyle >= (int)CoverLayoutStyle.NeonAccent;
        if (premium)
        {
            var bigNumberShape = OpenXmlPresentationHelpers.Shape(
                shapeId: context.NextShapeId(),
                shapeName: "SectionBigNumber",
                offsetX: dims.MarginX,
                offsetY: dims.MarginY,
                width: dims.ContentWidth,
                height: dims.ContentHeight,
                fill: null,
                paragraphs: new[]
                {
                    OpenXmlPresentationHelpers.Paragraph(
                        text: _sectionNumber.ToString("D2"),
                        runProperties: OpenXmlPresentationHelpers.RunProperties(
                            fontSizeHundredths: 14000,
                            colorHex: theme.Colors.AccentSoftHex,
                            bold: true,
                            fontFamily: theme.Fonts.TitleFamily),
                        paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Right))
                });
            tree.Append(bigNumberShape);
        }

        // Section number on top — small uppercase muted text.
        var sectionLabel = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "SectionLabel",
            offsetX: dims.MarginX,
            offsetY: dims.MarginY + (dims.ContentHeight / 3),
            width: dims.ContentWidth,
            height: OpenXmlPresentationHelpers.PxToEmu(40),
            fill: null,
            paragraphs: new[]
            {
                OpenXmlPresentationHelpers.Paragraph(
                    text: $"Section {_sectionNumber:D2}",
                    runProperties: OpenXmlPresentationHelpers.RunProperties(
                        fontSizeHundredths: 1400,
                        colorHex: theme.Colors.AccentHex,
                        bold: true,
                        fontFamily: theme.Fonts.BodyFamily),
                    paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left))
            });
        tree.Append(sectionLabel);

        // Section title.
        var titleY = dims.MarginY + (dims.ContentHeight / 3) + OpenXmlPresentationHelpers.PxToEmu(50);
        var titleShape = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "SectionTitle",
            offsetX: dims.MarginX,
            offsetY: titleY,
            width: dims.ContentWidth,
            height: OpenXmlPresentationHelpers.PxToEmu(180),
            fill: null,
            paragraphs: new[]
            {
                OpenXmlPresentationHelpers.Paragraph(
                    text: response.Title,
                    runProperties: OpenXmlPresentationHelpers.RunProperties(
                        fontSizeHundredths: 4400,
                        colorHex: theme.Colors.PrimaryHex,
                        bold: true,
                        fontFamily: theme.Fonts.TitleFamily),
                    paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left))
            });
        tree.Append(titleShape);

        // Accent underline.
        var underline = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "SectionAccent",
            offsetX: dims.MarginX,
            offsetY: titleY + OpenXmlPresentationHelpers.PxToEmu(200),
            width: OpenXmlPresentationHelpers.PxToEmu(96),
            height: OpenXmlPresentationHelpers.PxToEmu(6),
            fill: OpenXmlPresentationHelpers.SolidFill(theme.Colors.AccentHex),
            paragraphs: new[] { OpenXmlPresentationHelpers.EmptyParagraph() });
        tree.Append(underline);

        return 1;
    }
}
