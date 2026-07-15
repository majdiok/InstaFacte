using DocumentFormat.OpenXml;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;

/// <summary>
/// Generates an editable PPTX native table (<c>&lt;a:tbl&gt;</c>) wrapped in a <c>GraphicFrame</c>.
/// Tables created this way can be edited in PowerPoint, LibreOffice and Google Slides.
/// </summary>
internal static class TablePresentationBuilder
{
    public static P.GraphicFrame BuildTable(
        IPowerPointTheme theme,
        DashboardTableBlock block,
        long offsetX,
        long offsetY,
        long width,
        long height,
        uint shapeId)
    {
        // Truncate to a sensible size.
        var maxRows = Math.Min(block.Rows.Count, PowerPointDeckLimits.MaxRowsPerTableSlide);
        var maxCols = Math.Min(block.Columns.Count, PowerPointDeckLimits.MaxColumnsPerTableSlide);

        var columns = block.Columns.Take(maxCols).ToList();
        var rows = block.Rows.Take(maxRows).ToList();

        var colWidth = width / Math.Max(columns.Count, 1);
        var headerRowHeight = OpenXmlPresentationHelpers.PxToEmu(36);
        var bodyRowHeight = (height - headerRowHeight) / Math.Max(rows.Count, 1);
        if (bodyRowHeight < OpenXmlPresentationHelpers.PxToEmu(22))
            bodyRowHeight = OpenXmlPresentationHelpers.PxToEmu(22);

        var grid = new A.TableGrid();
        for (var i = 0; i < columns.Count; i++)
            grid.Append(new A.GridColumn { Width = colWidth });

        var table = new A.Table(
            new A.TableProperties(new A.TableStyleId { Text = "{5C22544A-7EE6-4342-B048-85BDC9FD1C3A}" })
            {
                FirstRow = true,
                BandRow = true
            },
            grid);

        // Header row.
        var headerRow = new A.TableRow { Height = headerRowHeight };
        foreach (var column in columns)
        {
            headerRow.Append(BuildCell(
                text: column.Label,
                isHeader: true,
                theme: theme));
        }
        table.Append(headerRow);

        // Body rows.
        var rowIndex = 0;
        foreach (var rowValues in rows)
        {
            var tableRow = new A.TableRow { Height = bodyRowHeight };
            var even = rowIndex % 2 == 0;
            for (var c = 0; c < columns.Count; c++)
            {
                var cellText = c < rowValues.Count ? rowValues[c] ?? string.Empty : string.Empty;
                tableRow.Append(BuildCell(cellText, isHeader: false, theme: theme, alternate: even));
            }
            table.Append(tableRow);
            rowIndex++;
        }

        // Graphic frame wraps the table for placement.
        var frame = new P.GraphicFrame(
            new P.NonVisualGraphicFrameProperties(
                new P.NonVisualDrawingProperties { Id = shapeId, Name = "DataTable" },
                new P.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.Transform(
                new A.Offset { X = offsetX, Y = offsetY },
                new A.Extents { Cx = width, Cy = height }),
            new A.Graphic(
                new A.GraphicData(table)
                {
                    Uri = "http://schemas.openxmlformats.org/drawingml/2006/table"
                }));
        return frame;
    }

    private static A.TableCell BuildCell(string text, bool isHeader, IPowerPointTheme theme, bool alternate = false)
    {
        var paragraph = OpenXmlPresentationHelpers.Paragraph(
            text: text,
            runProperties: OpenXmlPresentationHelpers.RunProperties(
                fontSizeHundredths: isHeader ? 1200 : 1100,
                colorHex: isHeader ? "FFFFFF" : theme.Colors.OnSurfaceHex,
                bold: isHeader,
                fontFamily: theme.Fonts.BodyFamily),
            paragraphProperties: OpenXmlPresentationHelpers.ParagraphProperties(A.TextAlignmentTypeValues.Left));

        var textBody = new A.TextBody(
            new A.BodyProperties { Anchor = A.TextAnchoringTypeValues.Center, LeftInset = 91440, RightInset = 91440, TopInset = 45720, BottomInset = 45720 },
            new A.ListStyle(),
            paragraph);

        var fill = isHeader
            ? OpenXmlPresentationHelpers.SolidFill(theme.Colors.PrimaryHex)
            : OpenXmlPresentationHelpers.SolidFill(alternate ? theme.Colors.SurfaceHex : theme.Colors.BackgroundHex);

        var lineColor = OpenXmlPresentationHelpers.SolidFill(theme.Colors.MutedHex);
        var cellProperties = new A.TableCellProperties(
            new A.LeftBorderLineProperties(lineColor) { Width = 6350 },
            new A.RightBorderLineProperties(lineColor.CloneNode(true)) { Width = 6350 },
            new A.TopBorderLineProperties(lineColor.CloneNode(true)) { Width = 6350 },
            new A.BottomBorderLineProperties(lineColor.CloneNode(true)) { Width = 6350 },
            fill);
        cellProperties.Anchor = A.TextAnchoringTypeValues.Center;

        return new A.TableCell(textBody, cellProperties);
    }
}
