using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using A = DocumentFormat.OpenXml.Drawing;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

/// <summary>
/// Renders one editable PPTX-native table per dashboard table block. Large tables are paginated
/// across multiple slides so every row stays readable.
/// </summary>
public sealed class TableSlideBuilder : IResponseSlideBuilder
{
    public int Build(SlideBuildContext context, AssistantResponseSlideModel response)
    {
        if (response.Tables.Count == 0)
            return 0;

        var slidesCreated = 0;
        foreach (var (table, idx) in response.Tables.Select((t, i) => (t, i)))
        {
            var pages = SplitIntoPages(table);
            foreach (var (page, pageIndex) in pages.Select((p, i) => (p, i)))
            {
                BuildSinglePageSlide(context, response, page, pageIndex, pages.Count, idx);
                slidesCreated++;
            }
        }

        return slidesCreated;
    }

    private static IReadOnlyList<DashboardTableBlock> SplitIntoPages(DashboardTableBlock block)
    {
        if (block.Rows.Count <= PowerPointDeckLimits.MaxRowsPerTableSlide)
            return new[] { block };

        var pages = new List<DashboardTableBlock>();
        for (var offset = 0; offset < block.Rows.Count; offset += PowerPointDeckLimits.MaxRowsPerTableSlide)
        {
            var subRows = block.Rows
                .Skip(offset)
                .Take(PowerPointDeckLimits.MaxRowsPerTableSlide)
                .ToList();
            pages.Add(block with { Rows = subRows });
        }
        return pages;
    }

    private static void BuildSinglePageSlide(
        SlideBuildContext context,
        AssistantResponseSlideModel response,
        DashboardTableBlock table,
        int pageIndex,
        int totalPages,
        int tableIndex)
    {
        var (_, tree, _) = SlideFactory.CreateSlide(context);

        var baseTitle = string.IsNullOrWhiteSpace(table.Title)
            ? $"{response.Title} — Tableau {tableIndex + 1}"
            : table.Title!;
        var title = totalPages > 1 ? $"{baseTitle} ({pageIndex + 1}/{totalPages})" : baseTitle;
        SlideHeaderFooterDecorator.AppendHeader(context, tree, title);

        var theme = context.Theme;
        var dims = context.Dimensions;

        var frame = TablePresentationBuilder.BuildTable(
            theme: theme,
            block: table,
            offsetX: dims.BodyLeft,
            offsetY: dims.BodyTop + OpenXmlPresentationHelpers.PxToEmu(10),
            width: dims.ContentWidth,
            height: dims.BodyHeight - OpenXmlPresentationHelpers.PxToEmu(20),
            shapeId: context.NextShapeId());
        tree.Append(frame);

        if (table.Columns.Count > PowerPointDeckLimits.MaxColumnsPerTableSlide)
        {
            var note = OpenXmlPresentationHelpers.Shape(
                shapeId: context.NextShapeId(),
                shapeName: "TableNote",
                offsetX: dims.BodyLeft,
                offsetY: dims.BodyTop + dims.BodyHeight - OpenXmlPresentationHelpers.PxToEmu(20),
                width: dims.ContentWidth,
                height: OpenXmlPresentationHelpers.PxToEmu(18),
                fill: null,
                paragraphs: new[]
                {
                    OpenXmlPresentationHelpers.Paragraph(
                        text: $"Note : {table.Columns.Count - PowerPointDeckLimits.MaxColumnsPerTableSlide} colonnes supplémentaires masquées pour la lisibilité.",
                        runProperties: OpenXmlPresentationHelpers.RunProperties(
                            fontSizeHundredths: 900,
                            colorHex: theme.Colors.MutedHex,
                            italic: true,
                            fontFamily: theme.Fonts.BodyFamily),
                        paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left))
                });
            tree.Append(note);
        }

        context.AppendContentFooter(tree);
    }
}
