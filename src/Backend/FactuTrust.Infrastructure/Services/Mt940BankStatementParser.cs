using System.Globalization;
using System.Text.RegularExpressions;
using FactuTrust.Application.DTOs;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Analyse un relevé SWIFT MT940 : tags <c>:25:</c> (compte), <c>:60F:</c>/<c>:62F:</c> (soldes
/// ouverture/clôture), <c>:61:</c> (opération), <c>:86:</c> (libellé). Les montants MT940 utilisent
/// la virgule décimale. AUCUNE persistance.
/// </summary>
public sealed class Mt940BankStatementParser
{
    private const int MaxRows = 50_000;

    // :61: valuedate(6) [entrydate(4)] mark(C/D/RC/RD) [funds(1)] amount(15d, virgule)
    private static readonly Regex Line61 = new(
        @"^(?<vdate>\d{6})(?<edate>\d{4})?(?<mark>RC|RD|C|D)(?<funds>[A-Z])?(?<amount>[\d,]+)",
        RegexOptions.Compiled);

    // :60F:C240501TND1234,56  → mark, YYMMDD, currency, amount
    private static readonly Regex Balance = new(
        @"^(?<mark>C|D)(?<date>\d{6})(?<ccy>[A-Z]{3})(?<amount>[\d,]+)",
        RegexOptions.Compiled);

    public BankFileParseResult Parse(byte[] content)
    {
        if (content is null || content.Length == 0)
            return Empty("Le fichier est vide.");

        string text;
        try
        {
            text = TabularFileParsing.DecodeText(content);
        }
        catch (Exception ex)
        {
            return Empty($"Lecture impossible : {ex.Message}");
        }

        var rawLines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (!text.Contains(":61:", StringComparison.Ordinal))
            return Empty("Fichier MT940 non reconnu (aucune opération :61: trouvée).");

        var issues = new List<ImportIssueDto>();
        var lines = new List<ImportBankStatementLineRequest>();
        string? account = null;
        decimal? opening = null;
        decimal? closing = null;

        string? currentTag = null;
        var buffer = new List<string>();
        // Transaction en cours (posée par un :61:, complétée par le :86: suivant).
        (DateTime Tx, DateTime? Value, decimal Amount, bool IsDebit, string Reference)? pending = null;
        var rowNumber = 0;

        void FlushPending(string description)
        {
            if (pending is null) return;
            var p = pending.Value;
            lines.Add(new ImportBankStatementLineRequest
            {
                TransactionDate = p.Tx,
                ValueDate = p.Value,
                Reference = p.Reference,
                Description = string.IsNullOrWhiteSpace(description) ? "Opération bancaire" : description.Trim(),
                Amount = p.Amount,
                IsDebit = p.IsDebit
            });
            pending = null;
        }

        foreach (var raw in rawLines)
        {
            var m = Regex.Match(raw, @"^:(?<tag>\d{2}[A-Z]?):(?<val>.*)$");
            if (m.Success)
            {
                // Nouveau tag : traiter le tag précédent bufferisé.
                ProcessTag(currentTag, string.Join(" ", buffer).Trim());
                currentTag = m.Groups["tag"].Value;
                buffer.Clear();
                buffer.Add(m.Groups["val"].Value);
            }
            else if (currentTag is not null)
            {
                buffer.Add(raw); // continuation (typiquement :86:)
            }
        }
        ProcessTag(currentTag, string.Join(" ", buffer).Trim());
        // Dernière opération sans :86: de clôture.
        FlushPending(string.Empty);

        void ProcessTag(string? tag, string value)
        {
            if (tag is null) return;
            switch (tag)
            {
                case "25":
                    account = value.Split('/').Last().Trim();
                    if (string.IsNullOrEmpty(account)) account = value.Trim();
                    break;
                case "60F":
                case "60M":
                    opening ??= ParseBalance(value);
                    break;
                case "62F":
                case "62M":
                    closing = ParseBalance(value) ?? closing;
                    break;
                case "61":
                    // Une nouvelle opération : d'abord clore la précédente si son :86: n'est pas venu.
                    FlushPending(string.Empty);
                    if (lines.Count >= MaxRows)
                    {
                        issues.Add(NonBlocking("fichier", $"Fichier tronqué : plus de {MaxRows} opérations."));
                        return;
                    }
                    rowNumber++;
                    var parsed = ParseLine61(value);
                    if (parsed is null)
                        issues.Add(Blocking($"opération {rowNumber}", $"Ligne :61: illisible : « {value} »."));
                    else
                        pending = parsed;
                    break;
                case "86":
                    FlushPending(value);
                    break;
            }
        }

        if (lines.Count == 0 && issues.All(x => !x.IsBlocking))
            issues.Add(Blocking("fichier", "Aucune opération exploitable dans le fichier MT940."));

        return new BankFileParseResult(lines, issues, account, account is { Length: 20 } && account.All(char.IsDigit) ? account : null,
            opening, closing, lines.Count > 0 ? lines.Max(l => l.TransactionDate) : null);
    }

    private static (DateTime Tx, DateTime? Value, decimal Amount, bool IsDebit, string Reference)? ParseLine61(string value)
    {
        var m = Line61.Match(value.Trim());
        if (!m.Success) return null;
        if (!TryParseSwiftDate(m.Groups["vdate"].Value, out var valueDate)) return null;

        DateTime txDate = valueDate;
        if (m.Groups["edate"].Success && m.Groups["edate"].Value.Length == 4)
        {
            // Date d'opération MMDD : réutilise l'année de la date de valeur (gère le passage d'année).
            var mm = int.Parse(m.Groups["edate"].Value[..2], CultureInfo.InvariantCulture);
            var dd = int.Parse(m.Groups["edate"].Value[2..], CultureInfo.InvariantCulture);
            var year = valueDate.Year;
            if (mm >= 1 && mm <= 12 && dd >= 1 && dd <= DateTime.DaysInMonth(year, mm))
                txDate = new DateTime(year, mm, dd);
        }

        var mark = m.Groups["mark"].Value;
        var isDebit = mark is "D" or "RD";
        if (!TabularFileParsing.TryParseAmount(m.Groups["amount"].Value, out var amount) || amount <= 0)
            return null;

        // Référence après le montant : segment jusqu'au « // » ou fin.
        var reference = string.Empty;
        var tail = value[(m.Index + m.Length)..];
        var refMatch = Regex.Match(tail, @"[A-Z]{4}(?<ref>[^/\s]+)");
        if (refMatch.Success) reference = refMatch.Groups["ref"].Value.Trim();

        return (txDate, valueDate, amount, isDebit, reference);
    }

    private static decimal? ParseBalance(string value)
    {
        var m = Balance.Match(value.Trim());
        if (!m.Success || !TabularFileParsing.TryParseAmount(m.Groups["amount"].Value, out var amount))
            return null;
        return m.Groups["mark"].Value == "D" ? -amount : amount;
    }

    private static bool TryParseSwiftDate(string yymmdd, out DateTime date)
    {
        date = default;
        if (yymmdd.Length != 6) return false;
        // YYMMDD : pivot standard 2000-2099.
        var full = "20" + yymmdd;
        return DateTime.TryParseExact(full, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static BankFileParseResult Empty(string message) =>
        new(Array.Empty<ImportBankStatementLineRequest>(), new[] { Blocking("fichier", message) });

    private static ImportIssueDto Blocking(string reference, string message) =>
        new() { Ref = reference, Message = message, IsBlocking = true };

    private static ImportIssueDto NonBlocking(string reference, string message) =>
        new() { Ref = reference, Message = message, IsBlocking = false };
}
