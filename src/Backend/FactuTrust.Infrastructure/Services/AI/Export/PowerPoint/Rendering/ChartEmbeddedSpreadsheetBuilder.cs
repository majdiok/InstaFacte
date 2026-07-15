using ClosedXML.Excel;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;

/// <summary>
/// Builds the minimal in-memory <c>.xlsx</c> spreadsheet that backs an embedded
/// <see cref="DocumentFormat.OpenXml.Packaging.ChartPart"/>, mirroring the layout PowerPoint
/// expects when a user clicks "Edit data in Excel". Strict PowerPoint consumers (2019+) refuse
/// charts that lack an <c>EmbeddedPackagePart</c> + <c>c:externalData</c> reference.
/// </summary>
/// <remarks>
/// Layout produced (Sheet1):
/// <code>
///   A1: (empty)        B1: Series1 label   C1: Series2 label   ...
///   A2: Label1         B2: value           C2: value           ...
///   A3: Label2         B3: value           C3: value           ...
/// </code>
/// Cell coordinates match the inline-cache references emitted by <see cref="ChartXmlBuilder"/>
/// (<c>Sheet1!$A$2:$A$N+1</c> for categories, <c>Sheet1!$B$2:$B$N+1</c> for first series, …).
/// </remarks>
internal static class ChartEmbeddedSpreadsheetBuilder
{
    public static byte[] BuildXlsxBytes(DashboardChartBlock block)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");

        // Row 1: series labels (B1..N1). A1 stays empty by convention.
        for (var s = 0; s < block.Series.Count; s++)
        {
            sheet.Cell(1, 2 + s).Value = block.Series[s].Label ?? $"Série {s + 1}";
        }

        // Rows 2..N+1: category label (column A) + series values (columns B..).
        for (var r = 0; r < block.Labels.Count; r++)
        {
            sheet.Cell(2 + r, 1).Value = block.Labels[r];
            for (var s = 0; s < block.Series.Count; s++)
            {
                var values = block.Series[s].Values;
                if (r >= values.Count) continue;
                var v = values[r];
                if (!v.HasValue) continue;
                if (double.IsNaN(v.Value) || double.IsInfinity(v.Value)) continue;
                sheet.Cell(2 + r, 2 + s).Value = v.Value;
            }
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
