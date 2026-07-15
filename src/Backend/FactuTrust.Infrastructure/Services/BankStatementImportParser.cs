using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using FactuTrust.Application.DTOs;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Analyse un fichier de relevé bancaire (CSV / Excel) en lignes d'opérations prêtes à être
/// importées, avec un rapport d'anomalies par ligne. AUCUNE donnée n'est persistée ici — la
/// création du relevé passe par <c>IBankReconciliationService.ImportStatementAsync</c>.
/// Colonnes acceptées : <c>date</c>, <c>libelle</c>, <c>reference</c> (optionnelle), puis soit
/// <c>debit</c>/<c>credit</c>, soit <c>montant</c> signé (positif = crédit/encaissement,
/// négatif = débit/décaissement — convention relevé côté client).
/// </summary>
public sealed class BankStatementImportParser
{
    /// <summary>Nombre maximal de lignes de données acceptées (garde-fou mémoire).</summary>
    private const int MaxRows = 50_000;

    private sealed record RawRow(int RowNumber, string Date, string Label, string Reference, string Debit, string Credit, string Amount);

    private static readonly Dictionary<string, string[]> ColumnSynonyms = new()
    {
        ["date"] = new[] { "date", "dateoperation", "datevaleur", "dateop", "datecomptable" },
        ["libelle"] = new[] { "libelle", "label", "description", "operation", "intitule", "designation" },
        ["reference"] = new[] { "reference", "ref", "refoperation", "numero", "piece", "numpiece" },
        ["debit"] = new[] { "debit", "retrait", "sortie", "decaissement" },
        ["credit"] = new[] { "credit", "versement", "depot", "entree", "encaissement" },
        ["montant"] = new[] { "montant", "amount", "valeur" }
    };

    public (IReadOnlyList<ImportBankStatementLineRequest> Lines, IReadOnlyList<ImportIssueDto> Issues) Parse(
        byte[] content, BankStatementFileFormat format)
    {
        if (content is null || content.Length == 0)
            return (Array.Empty<ImportBankStatementLineRequest>(), new[] { Issue("fichier", "Le fichier est vide.") });

        try
        {
            var rows = format switch
            {
                BankStatementFileFormat.Csv => ReadCsv(content),
                BankStatementFileFormat.Excel => ReadExcel(content),
                _ => (Rows: new List<RawRow>(), Issues: new List<ImportIssueDto> { Issue("format", "Format d'import inconnu.") })
            };

            if (rows.Issues.Count > 0 && rows.Rows.Count == 0)
                return (Array.Empty<ImportBankStatementLineRequest>(), rows.Issues);

            return BuildLines(rows.Rows, rows.Issues);
        }
        catch (Exception ex)
        {
            return (Array.Empty<ImportBankStatementLineRequest>(), new[] { Issue("fichier", $"Lecture impossible : {ex.Message}") });
        }
    }

    // ── Lecture brute par format ──────────────────────────────────────────────

    private static (List<RawRow> Rows, List<ImportIssueDto> Issues) ReadCsv(byte[] content)
    {
        var issues = new List<ImportIssueDto>();
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
        {
            issues.Add(Issue("fichier", "En-tête introuvable (première ligne)."));
            return (new List<RawRow>(), issues);
        }

        var header = csv.HeaderRecord ?? Array.Empty<string>();
        var map = TabularFileParsing.ResolveColumns(header, ColumnSynonyms);
        var missing = RequiredColumnsMissing(map);
        if (missing is not null)
        {
            issues.Add(Issue("en-tête", missing));
            return (new List<RawRow>(), issues);
        }

        var rows = new List<RawRow>();
        var rowNumber = 1;
        while (csv.Read())
        {
            rowNumber++;
            if (rows.Count >= MaxRows)
            {
                issues.Add(Issue("fichier", $"Fichier tronqué : plus de {MaxRows} lignes."));
                break;
            }
            string F(string key) => map.TryGetValue(key, out var i) && i >= 0 ? (csv.GetField(i) ?? string.Empty).Trim() : string.Empty;
            var raw = new RawRow(rowNumber, F("date"), F("libelle"), F("reference"), F("debit"), F("credit"), F("montant"));
            if (IsBlank(raw)) continue;
            rows.Add(raw);
        }

        return (rows, issues);
    }

    private static (List<RawRow> Rows, List<ImportIssueDto> Issues) ReadExcel(byte[] content)
    {
        var issues = new List<ImportIssueDto>();
        using var stream = new MemoryStream(content);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault();
        if (sheet is null)
        {
            issues.Add(Issue("fichier", "Classeur Excel vide."));
            return (new List<RawRow>(), issues);
        }

        var used = sheet.RangeUsed();
        if (used is null)
        {
            issues.Add(Issue("fichier", "Feuille Excel vide."));
            return (new List<RawRow>(), issues);
        }

        var rowsUsed = used.RowsUsed().ToList();
        if (rowsUsed.Count < 2)
        {
            issues.Add(Issue("fichier", "Aucune ligne de données sous l'en-tête."));
            return (new List<RawRow>(), issues);
        }

        var header = rowsUsed[0].Cells().Select(c => c.GetString()).ToArray();
        var map = TabularFileParsing.ResolveColumns(header, ColumnSynonyms);
        var missing = RequiredColumnsMissing(map);
        if (missing is not null)
        {
            issues.Add(Issue("en-tête", missing));
            return (new List<RawRow>(), issues);
        }

        var rows = new List<RawRow>();
        for (var r = 1; r < rowsUsed.Count; r++)
        {
            if (rows.Count >= MaxRows)
            {
                issues.Add(Issue("fichier", $"Fichier tronqué : plus de {MaxRows} lignes."));
                break;
            }
            var cells = rowsUsed[r];
            string F(string key)
            {
                if (!map.TryGetValue(key, out var i) || i < 0) return string.Empty;
                var cell = cells.Cell(i + 1);
                // Dates Excel : renvoyer un ISO parseable, sinon la valeur texte.
                if (key == "date" && cell.DataType == XLDataType.DateTime)
                    return cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return cell.GetString().Trim();
            }
            var raw = new RawRow(r + 1, F("date"), F("libelle"), F("reference"), F("debit"), F("credit"), F("montant"));
            if (IsBlank(raw)) continue;
            rows.Add(raw);
        }

        return (rows, issues);
    }

    // ── Construction des lignes d'opérations ─────────────────────────────────

    private static (IReadOnlyList<ImportBankStatementLineRequest> Lines, IReadOnlyList<ImportIssueDto> Issues) BuildLines(
        List<RawRow> rows, List<ImportIssueDto> issues)
    {
        var lines = new List<ImportBankStatementLineRequest>();
        if (rows.Count == 0)
        {
            if (issues.Count == 0)
                issues.Add(Issue("fichier", "Aucune ligne de données exploitable."));
            return (lines, issues);
        }

        foreach (var row in rows)
        {
            var date = TabularFileParsing.TryParseDate(row.Date);
            if (date is null)
            {
                issues.Add(Issue($"ligne {row.RowNumber}", $"Date illisible : « {row.Date} » (formats acceptés : AAAAMMJJ, AAAA-MM-JJ, JJ/MM/AAAA)."));
                continue;
            }

            var label = row.Label.Trim();
            if (string.IsNullOrEmpty(label))
            {
                issues.Add(Issue($"ligne {row.RowNumber}", "Libellé manquant."));
                continue;
            }

            decimal amount;
            bool isDebit;
            var hasSignedAmount = !string.IsNullOrWhiteSpace(row.Amount);
            if (hasSignedAmount)
            {
                if (!TabularFileParsing.TryParseAmount(row.Amount, out var signed))
                {
                    issues.Add(Issue($"ligne {row.RowNumber}", $"Montant illisible : « {row.Amount} »."));
                    continue;
                }
                if (signed == 0)
                {
                    issues.Add(Issue($"ligne {row.RowNumber}", "Montant nul."));
                    continue;
                }
                // Convention relevé côté client : positif = crédit (encaissement), négatif = débit.
                isDebit = signed < 0;
                amount = Math.Abs(signed);
            }
            else
            {
                if (!TabularFileParsing.TryParseAmount(row.Debit, out var debit))
                {
                    issues.Add(Issue($"ligne {row.RowNumber}", $"Débit illisible : « {row.Debit} »."));
                    continue;
                }
                if (!TabularFileParsing.TryParseAmount(row.Credit, out var credit))
                {
                    issues.Add(Issue($"ligne {row.RowNumber}", $"Crédit illisible : « {row.Credit} »."));
                    continue;
                }
                if (debit < 0 || credit < 0)
                {
                    issues.Add(Issue($"ligne {row.RowNumber}", "Les montants doivent être positifs ou nuls."));
                    continue;
                }
                if (debit > 0 && credit > 0)
                {
                    issues.Add(Issue($"ligne {row.RowNumber}", "Une ligne ne peut pas avoir simultanément un débit et un crédit."));
                    continue;
                }
                if (debit == 0 && credit == 0)
                {
                    issues.Add(Issue($"ligne {row.RowNumber}", "Chaque ligne doit avoir un débit ou un crédit."));
                    continue;
                }
                isDebit = debit > 0;
                amount = isDebit ? debit : credit;
            }

            lines.Add(new ImportBankStatementLineRequest
            {
                TransactionDate = date.Value,
                Reference = row.Reference.Trim(),
                Description = label,
                Amount = amount,
                IsDebit = isDebit
            });
        }

        return (lines, issues);
    }

    private static string? RequiredColumnsMissing(Dictionary<string, int> map)
    {
        if (map["date"] < 0)
            return "Colonne obligatoire absente : date. Colonnes attendues : date, libelle, [reference], debit/credit ou montant.";
        if (map["libelle"] < 0)
            return "Colonne obligatoire absente : libelle. Colonnes attendues : date, libelle, [reference], debit/credit ou montant.";
        var hasAmounts = map["montant"] >= 0 || map["debit"] >= 0 || map["credit"] >= 0;
        if (!hasAmounts)
            return "Colonnes de montants absentes : fournir debit/credit ou une colonne montant (signée).";
        return null;
    }

    private static bool IsBlank(RawRow r) =>
        string.IsNullOrWhiteSpace(r.Date) && string.IsNullOrWhiteSpace(r.Label) &&
        string.IsNullOrWhiteSpace(r.Debit) && string.IsNullOrWhiteSpace(r.Credit) &&
        string.IsNullOrWhiteSpace(r.Amount);

    private static ImportIssueDto Issue(string reference, string message) =>
        new() { Ref = reference, Message = message, IsBlocking = true };
}
