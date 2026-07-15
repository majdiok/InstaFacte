using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using FactuTrust.Application.DTOs;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Analyse un fichier de reprise (CSV / Excel / FEC) en écritures reconstituées, avec un rapport
/// d'anomalies par ligne. AUCUNE écriture n'est persistée ici — la validation métier et la
/// persistance sont assurées par <see cref="JournalImportService"/>.
/// </summary>
public sealed class JournalImportParser
{
    /// <summary>Nombre maximal de lignes de données acceptées (garde-fou mémoire).</summary>
    private const int MaxRows = 50_000;

    private sealed record RawRow(int RowNumber, string Journal, string Piece, string Date, string Account, string Label, string Debit, string Credit);

    public (IReadOnlyList<ImportEntryDto> Entries, IReadOnlyList<ImportIssueDto> Issues) Parse(byte[] content, JournalImportFormat format)
    {
        if (content is null || content.Length == 0)
            return (Array.Empty<ImportEntryDto>(), new[] { Issue("fichier", "Le fichier est vide.") });

        try
        {
            var rows = format switch
            {
                JournalImportFormat.Csv => ReadCsv(content),
                JournalImportFormat.Excel => ReadExcel(content),
                JournalImportFormat.Fec => ReadFec(content),
                _ => (Rows: new List<RawRow>(), Issues: new List<ImportIssueDto> { Issue("format", "Format d'import inconnu.") })
            };

            if (rows.Issues.Count > 0 && rows.Rows.Count == 0)
                return (Array.Empty<ImportEntryDto>(), rows.Issues);

            return BuildEntries(rows.Rows, rows.Issues);
        }
        catch (Exception ex)
        {
            return (Array.Empty<ImportEntryDto>(), new[] { Issue("fichier", $"Lecture impossible : {ex.Message}") });
        }
    }

    // ── Lecture brute par format ──────────────────────────────────────────────

    private static (List<RawRow> Rows, List<ImportIssueDto> Issues) ReadCsv(byte[] content)
    {
        var issues = new List<ImportIssueDto>();
        var text = DecodeText(content);
        var delimiter = DetectDelimiter(text);

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
        var map = ResolveColumns(header);
        var missing = RequiredColumnsMissing(map);
        if (missing is not null)
        {
            issues.Add(Issue("en-tête", $"Colonne obligatoire absente : {missing}. Colonnes attendues : journal, numero, date, compte, libelle, debit, credit."));
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
            var raw = new RawRow(rowNumber, F("journal"), F("numero"), F("date"), F("compte"), F("libelle"), F("debit"), F("credit"));
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
        var map = ResolveColumns(header);
        var missing = RequiredColumnsMissing(map);
        if (missing is not null)
        {
            issues.Add(Issue("en-tête", $"Colonne obligatoire absente : {missing}. Colonnes attendues : journal, numero, date, compte, libelle, debit, credit."));
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
            var raw = new RawRow(r + 1, F("journal"), F("numero"), F("date"), F("compte"), F("libelle"), F("debit"), F("credit"));
            if (IsBlank(raw)) continue;
            rows.Add(raw);
        }

        return (rows, issues);
    }

    private static (List<RawRow> Rows, List<ImportIssueDto> Issues) ReadFec(byte[] content)
    {
        // FEC : tabulé, en-tête en première ligne. Positions fixes (cf. FecExportService).
        // 0 JournalCode | 2 EcritureNum | 3 EcritureDate | 4 CompteNum | 10 EcritureLib | 11 Debit | 12 Credit
        var issues = new List<ImportIssueDto>();
        var text = DecodeText(content);
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (lines.Length < 2)
        {
            issues.Add(Issue("fichier", "Fichier FEC sans données."));
            return (new List<RawRow>(), issues);
        }

        var rows = new List<RawRow>();
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            if (rows.Count >= MaxRows)
            {
                issues.Add(Issue("fichier", $"Fichier tronqué : plus de {MaxRows} lignes."));
                break;
            }
            var f = lines[i].Split('\t');
            if (f.Length < 13)
            {
                issues.Add(Issue($"ligne {i + 1}", "Ligne FEC incomplète (moins de 13 colonnes)."));
                continue;
            }
            var raw = new RawRow(i + 1, f[0].Trim(), f[2].Trim(), f[3].Trim(), f[4].Trim(), f[10].Trim(), f[11].Trim(), f[12].Trim());
            if (IsBlank(raw)) continue;
            rows.Add(raw);
        }

        return (rows, issues);
    }

    // ── Reconstitution des écritures ──────────────────────────────────────────

    private static (IReadOnlyList<ImportEntryDto> Entries, IReadOnlyList<ImportIssueDto> Issues) BuildEntries(
        List<RawRow> rows, List<ImportIssueDto> issues)
    {
        var entries = new List<ImportEntryDto>();
        if (rows.Count == 0)
        {
            if (issues.Count == 0)
                issues.Add(Issue("fichier", "Aucune ligne de données exploitable."));
            return (entries, issues);
        }

        // Regroupement par (journal, pièce) en conservant l'ordre d'apparition.
        var groups = new Dictionary<string, (string Journal, string Piece, DateTime? Date, string Label, List<ImportEntryLineDto> Lines, int Order)>();
        var order = 0;

        foreach (var row in rows)
        {
            var journal = row.Journal.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(journal))
            {
                issues.Add(Issue($"ligne {row.RowNumber}", "Code journal manquant."));
                continue;
            }

            var account = row.Account.Trim();
            if (string.IsNullOrEmpty(account))
            {
                issues.Add(Issue($"ligne {row.RowNumber}", "Compte manquant."));
                continue;
            }

            var piece = string.IsNullOrWhiteSpace(row.Piece) ? "1" : row.Piece.Trim();
            var date = TryParseDate(row.Date);
            if (date is null)
            {
                issues.Add(Issue($"ligne {row.RowNumber}", $"Date illisible : « {row.Date} » (formats acceptés : AAAAMMJJ, AAAA-MM-JJ, JJ/MM/AAAA)."));
                continue;
            }

            if (!TryParseAmount(row.Debit, out var debit))
            {
                issues.Add(Issue($"ligne {row.RowNumber}", $"Débit illisible : « {row.Debit} »."));
                continue;
            }
            if (!TryParseAmount(row.Credit, out var credit))
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

            var key = journal + "" + piece;
            if (!groups.TryGetValue(key, out var group))
            {
                group = (journal, piece, date, string.IsNullOrWhiteSpace(row.Label) ? $"Reprise {journal}-{piece}" : row.Label.Trim(),
                    new List<ImportEntryLineDto>(), order++);
                groups[key] = group;
            }

            group.Lines.Add(new ImportEntryLineDto
            {
                AccountNumber = account,
                Label = string.IsNullOrWhiteSpace(row.Label) ? group.Label : row.Label.Trim(),
                Debit = debit,
                Credit = credit
            });
            groups[key] = group;
        }

        foreach (var g in groups.Values.OrderBy(x => x.Order))
        {
            var totalDebit = g.Lines.Sum(l => l.Debit);
            var totalCredit = g.Lines.Sum(l => l.Credit);
            var balanced = Math.Round(totalDebit, 3) == Math.Round(totalCredit, 3);

            entries.Add(new ImportEntryDto
            {
                Ref = $"{g.Journal}/{g.Piece}",
                Piece = g.Piece,
                JournalCode = g.Journal,
                EntryDate = g.Date!.Value,
                Label = g.Label,
                TotalDebit = totalDebit,
                TotalCredit = totalCredit,
                IsBalanced = balanced,
                Lines = g.Lines
            });
        }

        return (entries, issues);
    }

    // ── Utilitaires ───────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string[]> ColumnSynonyms = new()
    {
        ["journal"] = new[] { "journal", "journalcode", "codejournal", "jrn", "jal" },
        ["numero"] = new[] { "numero", "num", "piece", "no", "ecriturenum", "npiece", "numpiece" },
        ["date"] = new[] { "date", "ecrituredate", "datepiece", "dateecriture" },
        ["compte"] = new[] { "compte", "comptenum", "account", "ncompte", "numcompte" },
        ["libelle"] = new[] { "libelle", "label", "ecriturelib", "intitule", "designation" },
        ["debit"] = new[] { "debit" },
        ["credit"] = new[] { "credit" }
    };

    private static Dictionary<string, int> ResolveColumns(IReadOnlyList<string> header)
        => TabularFileParsing.ResolveColumns(header, ColumnSynonyms);

    private static string? RequiredColumnsMissing(Dictionary<string, int> map)
    {
        // journal/date/piece peuvent manquer (valeurs par défaut), mais compte + montants sont obligatoires.
        if (map["compte"] < 0) return "compte";
        if (map["debit"] < 0) return "debit";
        if (map["credit"] < 0) return "credit";
        return null;
    }

    private static bool IsBlank(RawRow r) =>
        string.IsNullOrWhiteSpace(r.Journal) && string.IsNullOrWhiteSpace(r.Account) &&
        string.IsNullOrWhiteSpace(r.Debit) && string.IsNullOrWhiteSpace(r.Credit) &&
        string.IsNullOrWhiteSpace(r.Label);

    // Aides génériques (décodage, délimiteur, dates, montants) : cf. TabularFileParsing,
    // partagées avec le parser de relevés bancaires — comportement inchangé.
    private static string DecodeText(byte[] content) => TabularFileParsing.DecodeText(content);
    private static string DetectDelimiter(string text) => TabularFileParsing.DetectDelimiter(text);
    private static DateTime? TryParseDate(string value) => TabularFileParsing.TryParseDate(value);
    private static bool TryParseAmount(string value, out decimal amount) => TabularFileParsing.TryParseAmount(value, out amount);

    private static ImportIssueDto Issue(string reference, string message) =>
        new() { Ref = reference, Message = message, IsBlocking = true };
}
