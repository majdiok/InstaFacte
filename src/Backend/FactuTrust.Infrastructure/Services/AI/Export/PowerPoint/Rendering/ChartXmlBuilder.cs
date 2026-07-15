using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using P = DocumentFormat.OpenXml.Presentation;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;

/// <summary>
/// Builds a native PPTX <see cref="ChartPart"/> (editable in PowerPoint) from a
/// <see cref="DashboardChartBlock"/>. Supports bar / column / line / pie / doughnut charts —
/// other types fall back to the closest supported variant so the export never fails.
/// </summary>
internal static class ChartXmlBuilder
{
    private const string EmbeddedXlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Attaches a chart part to <paramref name="slidePart"/> and returns a graphic frame
    /// positioned according to the supplied geometry.</summary>
    public static P.GraphicFrame AppendChart(
        SlidePart slidePart,
        IPowerPointTheme theme,
        DashboardChartBlock block,
        long offsetX,
        long offsetY,
        long width,
        long height,
        uint shapeId,
        CultureInfo culture)
    {
        var chartPart = slidePart.AddNewPart<ChartPart>();

        // Embed a minimal .xlsx that backs the chart data. PowerPoint 2019+ rejects charts
        // without an EmbeddedPackagePart + c:externalData reference; older versions accept
        // them but show a degraded "edit data" experience.
        var xlsxBytes = ChartEmbeddedSpreadsheetBuilder.BuildXlsxBytes(block);
        var embeddedXlsx = chartPart.AddEmbeddedPackagePart(EmbeddedXlsxContentType);
        using (var xlsxStream = new MemoryStream(xlsxBytes, writable: false))
        {
            embeddedXlsx.FeedData(xlsxStream);
        }
        var externalDataRelId = chartPart.GetIdOfPart(embeddedXlsx);

        chartPart.ChartSpace = BuildChartSpace(theme, block, culture, externalDataRelId);

        var relId = slidePart.GetIdOfPart(chartPart);

        return new P.GraphicFrame(
            new P.NonVisualGraphicFrameProperties(
                new P.NonVisualDrawingProperties { Id = shapeId, Name = "Chart" },
                new P.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.Transform(
                new A.Offset { X = offsetX, Y = offsetY },
                new A.Extents { Cx = width, Cy = height }),
            new A.Graphic(
                new A.GraphicData(
                    new C.ChartReference { Id = relId })
                {
                    Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart"
                }));
    }

    private static C.ChartSpace BuildChartSpace(
        IPowerPointTheme theme,
        DashboardChartBlock block,
        CultureInfo culture,
        string? externalDataRelId)
    {
        var resolvedType = ResolveChartType(block.ChartType);
        var plotArea = new C.PlotArea(new C.Layout());

        if (resolvedType is ChartKind.Pie or ChartKind.Doughnut)
        {
            plotArea.Append(BuildPieChart(theme, block, resolvedType == ChartKind.Doughnut));
        }
        else if (resolvedType is ChartKind.Line)
        {
            plotArea.Append(BuildLineChart(theme, block));
            AppendCategoryAndValueAxes(plotArea);
        }
        else if (resolvedType is ChartKind.Bar)
        {
            plotArea.Append(BuildBarChart(theme, block, isHorizontal: true));
            AppendCategoryAndValueAxes(plotArea, isBar: true);
        }
        else
        {
            plotArea.Append(BuildBarChart(theme, block, isHorizontal: false));
            AppendCategoryAndValueAxes(plotArea);
        }

        var chart = new C.Chart(
            new C.Title(
                new C.ChartText(new C.RichText(
                    new A.BodyProperties { Rotation = 0, UseParagraphSpacing = true, VerticalOverflow = A.TextVerticalOverflowValues.Ellipsis, Wrap = A.TextWrappingValues.Square, Anchor = A.TextAnchoringTypeValues.Center, AnchorCenter = true },
                    new A.ListStyle(),
                    new A.Paragraph(
                        new A.ParagraphProperties(),
                        new A.Run(
                            new A.RunProperties { Bold = true, FontSize = 1600, Language = "fr-FR" },
                            new A.Text(string.IsNullOrWhiteSpace(block.Title) ? "Graphique" : block.Title!))))),
                new C.Overlay { Val = false }),
            new C.AutoTitleDeleted { Val = false },
            plotArea,
            new C.Legend(
                new C.LegendPosition { Val = C.LegendPositionValues.Bottom },
                new C.Overlay { Val = false }),
            new C.PlotVisibleOnly { Val = true },
            new C.DisplayBlanksAs { Val = C.DisplayBlanksAsValues.Gap });

        var chartSpace = new C.ChartSpace(
            new C.EditingLanguage { Val = culture.Name },
            chart);

        if (externalDataRelId is not null)
        {
            chartSpace.Append(new C.ExternalData(
                new C.AutoUpdate { Val = false })
            {
                Id = externalDataRelId
            });
        }

        return chartSpace;
    }

    private static C.BarChart BuildBarChart(IPowerPointTheme theme, DashboardChartBlock block, bool isHorizontal)
    {
        var chart = new C.BarChart(
            new C.BarDirection { Val = isHorizontal ? C.BarDirectionValues.Bar : C.BarDirectionValues.Column },
            new C.BarGrouping { Val = C.BarGroupingValues.Clustered });

        var serIndex = 0;
        foreach (var series in block.Series)
        {
            chart.Append(BuildBarSeries(theme, block, series, serIndex));
            serIndex++;
        }

        chart.Append(new C.GapWidth { Val = 150 });
        chart.Append(new C.AxisId { Val = 111111111U });
        chart.Append(new C.AxisId { Val = 222222222U });
        return chart;
    }

    private static C.BarChartSeries BuildBarSeries(IPowerPointTheme theme, DashboardChartBlock block, DashboardChartSeries series, int index)
    {
        var color = SelectSeriesColor(theme, index);

        return new C.BarChartSeries(
            new C.Index { Val = (uint)index },
            new C.Order { Val = (uint)index },
            new C.SeriesText(new C.NumericValue(series.Label)),
            new C.ChartShapeProperties(OpenXmlPresentationHelpers.SolidFill(color), new A.Outline(OpenXmlPresentationHelpers.NoFill())),
            BuildCategories(block.Labels),
            BuildNumericValues(series.Values));
    }

    private static C.LineChart BuildLineChart(IPowerPointTheme theme, DashboardChartBlock block)
    {
        var chart = new C.LineChart(
            new C.Grouping { Val = C.GroupingValues.Standard },
            new C.VaryColors { Val = false });

        var serIndex = 0;
        foreach (var series in block.Series)
        {
            chart.Append(BuildLineSeries(theme, block, series, serIndex));
            serIndex++;
        }

        chart.Append(new C.Smooth { Val = false });
        chart.Append(new C.AxisId { Val = 111111111U });
        chart.Append(new C.AxisId { Val = 222222222U });
        return chart;
    }

    private static C.LineChartSeries BuildLineSeries(IPowerPointTheme theme, DashboardChartBlock block, DashboardChartSeries series, int index)
    {
        var color = SelectSeriesColor(theme, index);

        return new C.LineChartSeries(
            new C.Index { Val = (uint)index },
            new C.Order { Val = (uint)index },
            new C.SeriesText(new C.NumericValue(series.Label)),
            new C.ChartShapeProperties(
                new A.Outline(OpenXmlPresentationHelpers.SolidFill(color)) { Width = 25400 }),
            BuildCategories(block.Labels),
            BuildNumericValues(series.Values));
    }

    private static C.PieChart BuildPieChart(IPowerPointTheme theme, DashboardChartBlock block, bool doughnut)
    {
        var chart = new C.PieChart(new C.VaryColors { Val = true });

        // Pie/doughnut uses only the first series.
        var primary = block.Series.FirstOrDefault();
        if (primary is null)
            return chart;

        chart.Append(BuildPieSeries(theme, block, primary));

        // Note: a true doughnut chart would use C.DoughnutChart, but here we keep the pie shape
        // for portability (LibreOffice rendering quirks). The hole effect can be added later.
        _ = doughnut;
        return chart;
    }

    private static C.PieChartSeries BuildPieSeries(IPowerPointTheme theme, DashboardChartBlock block, DashboardChartSeries series)
    {
        var pieSeries = new C.PieChartSeries(
            new C.Index { Val = 0U },
            new C.Order { Val = 0U },
            new C.SeriesText(new C.NumericValue(series.Label ?? "Série")));

        for (var i = 0; i < block.Labels.Count; i++)
        {
            pieSeries.Append(new C.DataPoint(
                new C.Index { Val = (uint)i },
                new C.Bubble3D { Val = false },
                new C.ChartShapeProperties(OpenXmlPresentationHelpers.SolidFill(SelectSeriesColor(theme, i)))));
        }

        pieSeries.Append(BuildCategories(block.Labels));
        pieSeries.Append(BuildNumericValues(series.Values));
        return pieSeries;
    }

    private static C.CategoryAxisData BuildCategories(IReadOnlyList<string> labels)
    {
        var stringRef = new C.StringReference(
            new C.Formula("Sheet1!$A$2:$A$" + (labels.Count + 1)),
            new C.StringCache(new C.PointCount { Val = (uint)labels.Count }));

        for (var i = 0; i < labels.Count; i++)
        {
            stringRef.StringCache!.Append(new C.StringPoint(new C.NumericValue(labels[i])) { Index = (uint)i });
        }

        return new C.CategoryAxisData(stringRef);
    }

    private static C.Values BuildNumericValues(IReadOnlyList<double?> values)
    {
        var numRef = new C.NumberReference(
            new C.Formula("Sheet1!$B$2:$B$" + (values.Count + 1)),
            new C.NumberingCache(new C.FormatCode("General"), new C.PointCount { Val = (uint)values.Count }));

        for (var i = 0; i < values.Count; i++)
        {
            var v = values[i];
            if (!v.HasValue || double.IsNaN(v.Value) || double.IsInfinity(v.Value))
                continue;

            numRef.NumberingCache!.Append(new C.NumericPoint(new C.NumericValue(v.Value.ToString("R", CultureInfo.InvariantCulture)))
            {
                Index = (uint)i
            });
        }

        return new C.Values(numRef);
    }

    private static void AppendCategoryAndValueAxes(C.PlotArea plotArea, bool isBar = false)
    {
        plotArea.Append(new C.CategoryAxis(
            new C.AxisId { Val = 111111111U },
            new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }),
            new C.Delete { Val = false },
            new C.AxisPosition { Val = isBar ? C.AxisPositionValues.Left : C.AxisPositionValues.Bottom },
            new C.CrossingAxis { Val = 222222222U }));

        plotArea.Append(new C.ValueAxis(
            new C.AxisId { Val = 222222222U },
            new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }),
            new C.Delete { Val = false },
            new C.AxisPosition { Val = isBar ? C.AxisPositionValues.Bottom : C.AxisPositionValues.Left },
            new C.CrossingAxis { Val = 111111111U }));
    }

    private static string SelectSeriesColor(IPowerPointTheme theme, int index)
    {
        var palette = theme.Colors.ChartSeriesHex;
        if (palette.Count == 0)
            return theme.Colors.AccentHex;
        return palette[index % palette.Count];
    }

    private enum ChartKind
    {
        Column,
        Bar,
        Line,
        Pie,
        Doughnut
    }

    private static ChartKind ResolveChartType(string? raw)
    {
        var normalized = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "bar" => ChartKind.Bar,
            "horizontalbar" => ChartKind.Bar,
            "horizontal_bar" => ChartKind.Bar,
            "line" => ChartKind.Line,
            "spline" => ChartKind.Line,
            "area" => ChartKind.Line,
            "pie" => ChartKind.Pie,
            "doughnut" => ChartKind.Doughnut,
            "donut" => ChartKind.Doughnut,
            _ => ChartKind.Column
        };
    }
}
