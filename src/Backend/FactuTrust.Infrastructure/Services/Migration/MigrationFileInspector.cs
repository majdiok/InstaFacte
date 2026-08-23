using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using FactuTrust.Application.DTOs;
using System.Globalization;

namespace FactuTrust.Infrastructure.Services.Migration;

/// <summary>Forme brute d'un fichier tabulaire : en-têtes, nombre de lignes, délimiteur éventuel.</summary>
public sealed record MigrationFileShape(
    IReadOnlyList<string> Headers,
    int DataRowCount,
    string? Delimiter,
    int SheetCount);

/// <summary>
/// Inspection d'un fichier de migration (CSV / Excel) SANS dépendre du schéma canonique :
/// contrairement à <c>TabularRowReader</c> (qui impose ses synonymes), l'inspecteur rend
/// les en-têtes bruts pour le fingerprint et le mapping assisté. Mêmes règles de lecture que
/// l'existant (ClosedXML première feuille, CsvHelper avec détection de délimiteur).
/// Public : utilisé aussi par la couche API pour la détection de format à l'upload.
/// </summary>
public static class MigrationFileInspector
{
    /// <summary>Renvoie la forme du fichier, ou null si le fichier est illisible/vide.</summary>
    public static MigrationFileShape? Inspect(byte[] content, JournalImportFormat format)
    {
        try
        {
            return format == JournalImportFormat.Excel ? InspectExcel(content) : InspectCsv(content);
        }
        catch (Exception)
        {
            // Fichier corrompu ou format inattendu : l'appelant produit l'erreur de validation.
            return null;
        }
    }

    /// <summary>Déduit le format tabulaire du nom de fichier (défaut : CSV).</summary>
    public static JournalImportFormat DetectFormat(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".xls", StringComparison.OrdinalIgnoreCase)
            ? JournalImportFormat.Excel
            : JournalImportFormat.Csv;
    }

    private static MigrationFileShape InspectCsv(byte[] content)
    {
        var text = TabularFileParsing.DecodeText(content);
        var delimiter = TabularFileParsing.DetectDelimiter(text);
        using var reader = new StringReader(text);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
            DetectDelimiter = false
        };
        using var csv = new CsvReader(reader, config);
        if (!csv.Read() || !csv.ReadHeader())
            return new MigrationFileShape(Array.Empty<string>(), 0, delimiter, 1);

        var headers = (csv.HeaderRecord ?? Array.Empty<string>())
            .Select(h => h.Trim())
            .ToArray();

        var rows = 0;
        while (csv.Read())
        {
            if (rows >= TabularRowReader.MaxRows) break;
            if (Enumerable.Range(0, headers.Length).All(i => string.IsNullOrWhiteSpace(csv.GetField(i))))
                continue;
            rows++;
        }

        return new MigrationFileShape(headers, rows, delimiter, 1);
    }

    private static MigrationFileShape InspectExcel(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault();
        var used = sheet?.RangeUsed();
        if (used is null)
            return new MigrationFileShape(Array.Empty<string>(), 0, null, workbook.Worksheets.Count);

        var rowsUsed = used.RowsUsed().ToList();
        var headers = rowsUsed.Count == 0
            ? Array.Empty<string>()
            : rowsUsed[0].Cells().Select(c => c.GetString().Trim()).ToArray();

        var rows = 0;
        for (var r = 1; r < rowsUsed.Count; r++)
        {
            if (rows >= TabularRowReader.MaxRows) break;
            if (rowsUsed[r].Cells().All(c => string.IsNullOrWhiteSpace(c.GetString()))) continue;
            rows++;
        }

        return new MigrationFileShape(headers, rows, null, workbook.Worksheets.Count);
    }
}
