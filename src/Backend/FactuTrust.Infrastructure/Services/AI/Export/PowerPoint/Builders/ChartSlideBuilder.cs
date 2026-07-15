using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

/// <summary>
/// Renders one slide per chart in the dashboard. The chart itself is a native, editable PPTX
/// chart (created via <see cref="ChartXmlBuilder"/>), so it remains editable in PowerPoint,
/// LibreOffice Impress and Google Slides.
/// </summary>
public sealed class ChartSlideBuilder : IResponseSlideBuilder
{
    public int Build(SlideBuildContext context, AssistantResponseSlideModel response)
    {
        if (response.Charts.Count == 0)
            return 0;

        var slidesCreated = 0;
        foreach (var (chart, index) in response.Charts.Select((c, i) => (c, i)))
        {
            BuildChartSlide(context, response, chart, index);
            slidesCreated++;
        }

        return slidesCreated;
    }

    private static void BuildChartSlide(
        SlideBuildContext context,
        AssistantResponseSlideModel response,
        DashboardChartBlock chart,
        int chartIndex)
    {
        var (slidePart, tree, _) = SlideFactory.CreateSlide(context);

        var slideTitle = string.IsNullOrWhiteSpace(chart.Title)
            ? $"{response.Title} — Graphique {chartIndex + 1}"
            : chart.Title!;
        SlideHeaderFooterDecorator.AppendHeader(context, tree, slideTitle);

        var dims = context.Dimensions;

        var frame = ChartXmlBuilder.AppendChart(
            slidePart: slidePart,
            theme: context.Theme,
            block: chart,
            offsetX: dims.BodyLeft,
            offsetY: dims.BodyTop + OpenXmlPresentationHelpers.PxToEmu(10),
            width: dims.ContentWidth,
            height: dims.BodyHeight - OpenXmlPresentationHelpers.PxToEmu(20),
            shapeId: context.NextShapeId(),
            culture: context.Culture);

        tree.Append(frame);
        context.AppendContentFooter(tree);
    }
}
