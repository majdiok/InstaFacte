using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using A = DocumentFormat.OpenXml.Drawing;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

/// <summary>
/// Renders a slide of KPI cards laid out on a responsive grid (2×2 / 3×2 / 4×2 depending on the
/// number of cards). Each card shows the label, value (large bold) and optional trend chip.
/// </summary>
public sealed class KpiSlideBuilder : IResponseSlideBuilder
{
    public int Build(SlideBuildContext context, AssistantResponseSlideModel response)
    {
        if (response.Kpis.Count == 0)
            return 0;

        var (_, tree, _) = SlideFactory.CreateSlide(context);

        var slideTitle = string.IsNullOrWhiteSpace(response.DashboardTitle)
            ? $"{response.Title} — Indicateurs clés"
            : response.DashboardTitle!;
        SlideHeaderFooterDecorator.AppendHeader(context, tree, slideTitle);

        var theme = context.Theme;
        var dims = context.Dimensions;

        var kpis = response.Kpis.Take(8).ToList();
        var columns = kpis.Count switch
        {
            <= 2 => kpis.Count,
            <= 4 => 2,
            <= 6 => 3,
            _ => 4
        };
        var rows = (int)Math.Ceiling(kpis.Count / (double)columns);

        var gutter = OpenXmlPresentationHelpers.PxToEmu(16);
        var cardWidth = (dims.ContentWidth - ((columns - 1) * gutter)) / columns;
        var cardHeight = (dims.BodyHeight - ((rows - 1) * gutter)) / Math.Max(rows, 1);

        for (var i = 0; i < kpis.Count; i++)
        {
            var col = i % columns;
            var row = i / columns;

            var x = dims.BodyLeft + col * (cardWidth + gutter);
            var y = dims.BodyTop + row * (cardHeight + gutter);

            BuildCard(context, tree, theme, kpis[i], x, y, cardWidth, cardHeight);
        }

        context.AppendContentFooter(tree);
        return 1;
    }

    /// <summary>
    /// Premium-V2 themes (CoverStyle ≥ 8) opt into a richer card design with a left accent bar
    /// and a larger value. Legacy themes (CoverStyle 0–7) keep the original rendering.
    /// </summary>
    private static bool UsePremiumCardStyle(IPowerPointTheme theme) =>
        (int)theme.CoverStyle >= (int)CoverLayoutStyle.NeonAccent;

    private static void BuildCard(
        SlideBuildContext context,
        DocumentFormat.OpenXml.Presentation.ShapeTree tree,
        IPowerPointTheme theme,
        DashboardKpiBlock kpi,
        long offsetX,
        long offsetY,
        long width,
        long height)
    {
        var background = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "KpiCard",
            offsetX: offsetX,
            offsetY: offsetY,
            width: width,
            height: height,
            fill: OpenXmlPresentationHelpers.SolidFill(theme.Colors.SurfaceHex),
            paragraphs: new[] { OpenXmlPresentationHelpers.EmptyParagraph() },
            leftInsetEmu: OpenXmlPresentationHelpers.PxToEmu(16),
            rightInsetEmu: OpenXmlPresentationHelpers.PxToEmu(16),
            topInsetEmu: OpenXmlPresentationHelpers.PxToEmu(14),
            bottomInsetEmu: OpenXmlPresentationHelpers.PxToEmu(14));
        tree.Append(background);

        // Premium-V2 themes get a 4px left accent bar — adds visual rhythm without breaking layouts.
        var premium = UsePremiumCardStyle(theme);
        if (premium)
        {
            var accentBarWidth = OpenXmlPresentationHelpers.PxToEmu(4);
            var accentBar = OpenXmlPresentationHelpers.Shape(
                shapeId: context.NextShapeId(),
                shapeName: "KpiCardAccent",
                offsetX: offsetX,
                offsetY: offsetY,
                width: accentBarWidth,
                height: height,
                fill: OpenXmlPresentationHelpers.SolidFill(theme.Colors.AccentHex),
                paragraphs: new[] { OpenXmlPresentationHelpers.EmptyParagraph() });
            tree.Append(accentBar);
        }

        var label = string.IsNullOrWhiteSpace(kpi.Label) ? "Indicateur" : kpi.Label!.Trim();
        var value = string.IsNullOrWhiteSpace(kpi.Value) ? "—" : kpi.Value!.Trim();
        if (!string.IsNullOrWhiteSpace(kpi.Unit))
            value = $"{value} {kpi.Unit!.Trim()}";

        var labelShape = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "KpiLabel",
            offsetX: offsetX + OpenXmlPresentationHelpers.PxToEmu(20),
            offsetY: offsetY + OpenXmlPresentationHelpers.PxToEmu(20),
            width: width - OpenXmlPresentationHelpers.PxToEmu(40),
            height: OpenXmlPresentationHelpers.PxToEmu(28),
            fill: null,
            paragraphs: new[]
            {
                OpenXmlPresentationHelpers.Paragraph(
                    text: label,
                    runProperties: OpenXmlPresentationHelpers.RunProperties(
                        fontSizeHundredths: 1200,
                        colorHex: theme.Colors.MutedHex,
                        fontFamily: theme.Fonts.BodyFamily),
                    paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left))
            });
        tree.Append(labelShape);

        // Premium themes use a larger value font (44pt) for greater impact; legacy stays at 34pt.
        var valueFontSize = premium ? 4400 : 3400;
        var valueShape = OpenXmlPresentationHelpers.Shape(
            shapeId: context.NextShapeId(),
            shapeName: "KpiValue",
            offsetX: offsetX + OpenXmlPresentationHelpers.PxToEmu(20),
            offsetY: offsetY + OpenXmlPresentationHelpers.PxToEmu(52),
            width: width - OpenXmlPresentationHelpers.PxToEmu(40),
            height: OpenXmlPresentationHelpers.PxToEmu(70),
            fill: null,
            paragraphs: new[]
            {
                OpenXmlPresentationHelpers.Paragraph(
                    text: value,
                    runProperties: OpenXmlPresentationHelpers.RunProperties(
                        fontSizeHundredths: valueFontSize,
                        colorHex: theme.Colors.PrimaryHex,
                        bold: true,
                        fontFamily: theme.Fonts.TitleFamily),
                    paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left))
            });
        tree.Append(valueShape);

        if (!string.IsNullOrWhiteSpace(kpi.Trend))
        {
            var trendColor = ResolveTrendColor(theme, kpi.Trend!);
            var trendShape = OpenXmlPresentationHelpers.Shape(
                shapeId: context.NextShapeId(),
                shapeName: "KpiTrend",
                offsetX: offsetX + OpenXmlPresentationHelpers.PxToEmu(20),
                offsetY: offsetY + height - OpenXmlPresentationHelpers.PxToEmu(34),
                width: width - OpenXmlPresentationHelpers.PxToEmu(40),
                height: OpenXmlPresentationHelpers.PxToEmu(24),
                fill: null,
                paragraphs: new[]
                {
                    OpenXmlPresentationHelpers.Paragraph(
                        text: kpi.Trend!.Trim(),
                        runProperties: OpenXmlPresentationHelpers.RunProperties(
                            fontSizeHundredths: 1100,
                            colorHex: trendColor,
                            bold: true,
                            fontFamily: theme.Fonts.BodyFamily),
                        paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left))
                });
            tree.Append(trendShape);
        }
    }

    private static string ResolveTrendColor(IPowerPointTheme theme, string trend)
    {
        var lower = trend.Trim().ToLowerInvariant();
        if (lower.StartsWith("+") || lower.Contains("hausse") || lower.Contains("up"))
            return theme.Colors.SuccessHex;
        if (lower.StartsWith("-") || lower.Contains("baisse") || lower.Contains("down"))
            return theme.Colors.DangerHex;
        return theme.Colors.AccentHex;
    }
}
