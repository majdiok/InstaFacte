using DocumentFormat.OpenXml.Packaging;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

/// <summary>Appends third-party theme attribution when required by license.</summary>
public sealed class ThemeAttributionSlideBuilder
{
    public int Build(SlideBuildContext context)
    {
        var attribution = context.Theme.RequiredAttribution;
        if (attribution is null)
            return 0;

        var (_, tree, _) = SlideFactory.CreateSlide(context, "Crédits du thème visuel");
        var theme = context.Theme;
        var dims = context.Dimensions;

        var lines = new List<string> { "Crédits & licences du thème" };
        if (!string.IsNullOrWhiteSpace(attribution.AttributionText))
            lines.Add(attribution.AttributionText);
        lines.Add($"Source : {attribution.Source} — {attribution.License}");
        lines.AddRange(attribution.AssetCredits);

        var body = string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
        var shape = OpenXmlPresentationHelpers.Shape(
            context.NextShapeId(),
            "ThemeAttribution",
            dims.MarginX,
            dims.MarginY,
            dims.ContentWidth,
            dims.ContentHeight,
            fill: null,
            paragraphs: body.Split('\n').Select(line =>
                OpenXmlPresentationHelpers.Paragraph(
                    line,
                    OpenXmlPresentationHelpers.RunProperties(
                        fontSizeHundredths: 1400,
                        colorHex: theme.Colors.OnSurfaceHex,
                        fontFamily: theme.Fonts.BodyFamily),
                    OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left))).ToArray());

        tree.Append(shape);
        context.AppendContentFooter(tree);
        return 1;
    }
}
