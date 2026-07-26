using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using FactuTrust.Application.DTOs;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Lecture générique d'un fichier tabulaire (CSV / Excel) en lignes clé→valeur, pilotée par une
/// table de synonymes de colonnes. Extrait verbatim de <see cref="ReferenceDataImportService"/>
/// pour être partagé avec <see cref="AccountMappingTable"/> — comportement strictement identique.
/// </summary>
internal static class TabularRowReader
{
    public const int MaxRows = 50_000;

    public static (List<Dictionary<string, string>> Rows, List<ImportIssueDto> Issues) Read(
        byte[] content, JournalImportFormat format, IReadOnlyDictionary<string, string[]> synonyms,
        string[] required, string expected)
    {
        return format == JournalImportFormat.Excel
            ? ReadExcel(content, synonyms, required, expected)
            : ReadCsv(content, synonyms, required, expected);
    }

    private static (List<Dictionary<string, string>>, List<ImportIssueDto>) ReadCsv(
        byte[] content, IReadOnlyDictionary<string, string[]> synonyms, string[] required, string expected)
    {
        var issues = new List<ImportIssueDto>();
        var text = TabularFileParsing.DecodeText(content);
        using var reader = new StringReader(text);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = TabularFileParsing.DetectDelimiter(text),
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
            DetectDelimiter = false
        };
        using var csv = new CsvReader(reader, config);
        if (!csv.Read() || !csv.ReadHeader())
        {
            issues.Add(Blocking("fichier", "En-tête introuvable (première ligne)."));
            return (new(), issues);
        }

        var map = TabularFileParsing.ResolveColumns(csv.HeaderRecord ?? Array.Empty<string>(), synonyms);
        var missing = required.FirstOrDefault(r => map.GetValueOrDefault(r, -1) < 0);
        if (missing is not null)
        {
            issues.Add(Blocking("en-tête", $"Colonne obligatoire absente : {missing}. Colonnes attendues : {expected}."));
            return (new(), issues);
        }

        var rows = new List<Dictionary<string, string>>();
        while (csv.Read())
        {
            if (rows.Count >= MaxRows) { issues.Add(Blocking("fichier", $"Fichier tronqué : plus de {MaxRows} lignes.")); break; }
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, idx) in map)
                row[key] = idx >= 0 ? (csv.GetField(idx) ?? string.Empty).Trim() : string.Empty;
            if (row.Values.All(string.IsNullOrWhiteSpace)) continue;
            rows.Add(row);
        }
        return (rows, issues);
    }

    private static (List<Dictionary<string, string>>, List<ImportIssueDto>) ReadExcel(
        byte[] content, IReadOnlyDictionary<string, string[]> synonyms, string[] required, string expected)
    {
        var issues = new List<ImportIssueDto>();
        using var stream = new MemoryStream(content);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault();
        var used = sheet?.RangeUsed();
        if (used is null)
        {
            issues.Add(Blocking("fichier", "Classeur Excel vide."));
            return (new(), issues);
        }

        var rowsUsed = used.RowsUsed().ToList();
        if (rowsUsed.Count < 2)
        {
            issues.Add(Blocking("fichier", "Aucune ligne de données sous l'en-tête."));
            return (new(), issues);
        }

        var header = rowsUsed[0].Cells().Select(c => c.GetString()).ToArray();
        var map = TabularFileParsing.ResolveColumns(header, synonyms);
        var missing = required.FirstOrDefault(r => map.GetValueOrDefault(r, -1) < 0);
        if (missing is not null)
        {
            issues.Add(Blocking("en-tête", $"Colonne obligatoire absente : {missing}. Colonnes attendues : {expected}."));
            return (new(), issues);
        }

        var rows = new List<Dictionary<string, string>>();
        for (var r = 1; r < rowsUsed.Count; r++)
        {
            if (rows.Count >= MaxRows) { issues.Add(Blocking("fichier", $"Fichier tronqué : plus de {MaxRows} lignes.")); break; }
            var cells = rowsUsed[r];
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, idx) in map)
                row[key] = idx >= 0 ? cells.Cell(idx + 1).GetString().Trim() : string.Empty;
            if (row.Values.All(string.IsNullOrWhiteSpace)) continue;
            rows.Add(row);
        }
        return (rows, issues);
    }

    private static ImportIssueDto Blocking(string reference, string message) =>
        new() { Ref = reference, Message = message, IsBlocking = true };
}
