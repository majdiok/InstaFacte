using DocumentFormat.OpenXml.Packaging;
using FactuTrust.Domain.Constants;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

/// <summary>
/// Renders the deck cover (title slide). Layout:
/// - Accent bar at the top
/// - Optional logo (top-right)
/// - Centred title (very large)
/// - Subtitle below the title
/// - Author + generation date at the bottom
/// </summary>
public sealed class CoverSlideBuilder : ISlideBuilder
{
    public int Build(SlideBuildContext context)
    {
        var (slidePart, tree, _) = SlideFactory.CreateSlide(context);

        var theme = context.Theme;
        var dims = context.Dimensions;

        if (!context.UseHybridMasterBackground)
        {
            // Full-bleed background using a soft surface tint for premium feel.
            var background = OpenXmlPresentationHelpers.Shape(
                shapeId: context.NextShapeId(),
                shapeName: "CoverBackground",
                offsetX: 0,
                offsetY: 0,
                width: dims.Width,
                height: dims.Height,
                fill: OpenXmlPresentationHelpers.SolidFill(theme.Colors.SurfaceHex),
                paragraphs: new[] { OpenXmlPresentationHelpers.EmptyParagraph() });
            tree.Append(background);

            // Top accent bar.
            var accentBar = OpenXmlPresentationHelpers.Shape(
                shapeId: context.NextShapeId(),
                shapeName: "CoverAccentBar",
                offsetX: 0,
                offsetY: 0,
                width: dims.Width,
                height: OpenXmlPresentationHelpers.PxToEmu(8),
                fill: OpenXmlPresentationHelpers.SolidFill(theme.Colors.AccentHex),
                paragraphs: new[] { OpenXmlPresentationHelpers.EmptyParagraph() });
            tree.Append(accentBar);
        }

        // Optional logo bottom-left (subtle).
        if (theme.LogoOverlayPng is { Length: > 0 } logoBytes)
        {
            TryAppendLogo(slidePart, tree, context, logoBytes);
        }

        var title = string.IsNullOrWhiteSpace(context.Request.Title)
            ? $"Présentation {BrandConstants.Name}"
            : context.Request.Title;

        // Headline.
        var headlineY = dims.MarginY + (dims.ContentHeight / 3);
        var headlineShape = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "CoverTitle",
            offsetX: dims.MarginX,
            offsetY: headlineY,
            width: dims.ContentWidth,
            height: OpenXmlPresentationHelpers.PxToEmu(160),
            fill: null,
            paragraphs: new[]
            {
                OpenXmlPresentationHelpers.Paragraph(
                    text: title,
                    runProperties: OpenXmlPresentationHelpers.RunProperties(
                        fontSizeHundredths: 5400,
                        colorHex: theme.Colors.PrimaryHex,
                        bold: true,
                        fontFamily: theme.Fonts.TitleFamily),
                    paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left))
            });
        tree.Append(headlineShape);

        // Subtitle.
        if (!string.IsNullOrWhiteSpace(context.Request.Subtitle))
        {
            var subtitleShape = OpenXmlPresentationHelpers.Shape(
                shapeId: context.NextShapeId(),
                shapeName: "CoverSubtitle",
                offsetX: dims.MarginX,
                offsetY: headlineY + OpenXmlPresentationHelpers.PxToEmu(170),
                width: dims.ContentWidth,
                height: OpenXmlPresentationHelpers.PxToEmu(60),
                fill: null,
                paragraphs: new[]
                {
                    OpenXmlPresentationHelpers.Paragraph(
                        text: context.Request.Subtitle!,
                        runProperties: OpenXmlPresentationHelpers.RunProperties(
                            fontSizeHundredths: 2400,
                            colorHex: theme.Colors.SecondaryHex,
                            fontFamily: theme.Fonts.BodyFamily),
                        paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left))
                });
            tree.Append(subtitleShape);
        }

        // Author + generation date at the very bottom.
        var footerLine = string.Join(" • ", new[]
        {
            string.IsNullOrWhiteSpace(context.AuthorName) ? null : context.AuthorName,
            DateTime.Now.ToString("dd MMMM yyyy", context.Culture)
        }.Where(s => !string.IsNullOrEmpty(s)));

        var authorShape = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "CoverFooter",
            offsetX: dims.MarginX,
            offsetY: dims.Height - dims.MarginY - OpenXmlPresentationHelpers.PxToEmu(30),
            width: dims.ContentWidth,
            height: OpenXmlPresentationHelpers.PxToEmu(28),
            fill: null,
            paragraphs: new[]
            {
                OpenXmlPresentationHelpers.Paragraph(
                    text: footerLine,
                    runProperties: OpenXmlPresentationHelpers.RunProperties(
                        fontSizeHundredths: 1200,
                        colorHex: theme.Colors.MutedHex,
                        fontFamily: theme.Fonts.BodyFamily),
                    paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left))
            });
        tree.Append(authorShape);

        return 1;
    }

    private static void TryAppendLogo(SlidePart slidePart, P.ShapeTree tree, SlideBuildContext context, byte[] logoBytes)
    {
        try
        {
            var imagePart = slidePart.AddImagePart(ImagePartType.Png);
            using (var ms = new MemoryStream(logoBytes))
            {
                imagePart.FeedData(ms);
            }
            var relId = slidePart.GetIdOfPart(imagePart);

            var dims = context.Dimensions;
            const long logoWidthEmu = 1_524_000L;  // ~1.67"
            const long logoHeightEmu = 457_200L;   // ~0.5"

            var pic = new P.Picture(
                new P.NonVisualPictureProperties(
                    new P.NonVisualDrawingProperties { Id = context.NextShapeId(), Name = "InstaFactLogo" },
                    new P.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }),
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.BlipFill(
                    new A.Blip { Embed = relId },
                    new A.Stretch(new A.FillRectangle())),
                new P.ShapeProperties(
                    new A.Transform2D(
                        new A.Offset
                        {
                            X = dims.Width - dims.MarginX - logoWidthEmu,
                            Y = dims.MarginY
                        },
                        new A.Extents { Cx = logoWidthEmu, Cy = logoHeightEmu }),
                    new A.PresetGeometry { Preset = A.ShapeTypeValues.Rectangle }));
            tree.Append(pic);
        }
        catch
        {
            // Logo embedding is best-effort. A corrupt PNG must not break the export.
        }
    }
}
