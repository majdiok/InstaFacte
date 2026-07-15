using System.Globalization;
using System.Text.RegularExpressions;
using FactuTrust.Application.DTOs;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Résultat d'analyse d'un fichier de relevé bancaire structuré (OFX / MT940) : lignes + anomalies
/// + métadonnées détectées (compte, soldes, date). AUCUNE persistance.
/// </summary>
public sealed record BankFileParseResult(
    IReadOnlyList<ImportBankStatementLineRequest> Lines,
    IReadOnlyList<ImportIssueDto> Issues,
    string? DetectedAccount = null,
    string? DetectedRib = null,
    decimal? OpeningBalance = null,
    decimal? ClosingBalance = null,
    DateTime? StatementDate = null);

/// <summary>
/// Analyse un fichier OFX (Open Financial Exchange, 1.x SGML ou 2.x XML) en opérations bancaires.
/// Tolérant aux deux dialectes : les valeurs de balise sont lues jusqu'au prochain <c>&lt;</c> ou
/// saut de ligne, ce qui couvre l'absence de balises fermantes en SGML.
/// </summary>
public sealed class OfxBankStatementParser
{
    private const int MaxRows = 50_000;

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

        if (!text.Contains("<STMTTRN", StringComparison.OrdinalIgnoreCase))
            return Empty("Fichier OFX non reconnu (aucune opération <STMTTRN> trouvée).");

        var issues = new List<ImportIssueDto>();
        var lines = new List<ImportBankStatementLineRequest>();

        // Découpe par bloc opération. Le premier segment (avant le 1er <STMTTRN>) porte l'en-tête.
        var segments = Regex.Split(text, "<STMTTRN>", RegexOptions.IgnoreCase);
        var header = segments.Length > 0 ? segments[0] : string.Empty;

        var detectedAccount = FirstTag(header, "ACCTID");
        var detectedRib = detectedAccount is { Length: 20 } && detectedAccount.All(char.IsDigit) ? detectedAccount : null;
        var closingBalance = ParseAmount(FirstTag(header, "BALAMT"));
        // BALAMT peut aussi apparaître dans le dernier segment (LEDGERBAL après les opérations).
        if (closingBalance is null)
            closingBalance = ParseAmount(FirstTag(text, "BALAMT"));

        var rowNumber = 0;
        for (var i = 1; i < segments.Length; i++)
        {
            if (lines.Count >= MaxRows)
            {
                issues.Add(NonBlocking("fichier", $"Fichier tronqué : plus de {MaxRows} opérations."));
                break;
            }
            rowNumber++;
            var block = segments[i];

            var rawAmount = FirstTag(block, "TRNAMT");
            var amount = ParseAmount(rawAmount);
            if (amount is null || amount.Value == 0m)
            {
                issues.Add(Blocking($"opération {rowNumber}", $"Montant OFX illisible ou nul : « {rawAmount} »."));
                continue;
            }

            var date = ParseOfxDate(FirstTag(block, "DTPOSTED"));
            if (date is null)
            {
                issues.Add(Blocking($"opération {rowNumber}", "Date d'opération OFX (DTPOSTED) illisible."));
                continue;
            }
            var valueDate = ParseOfxDate(FirstTag(block, "DTUSER") ?? FirstTag(block, "DTAVAIL"));

            var name = FirstTag(block, "NAME") ?? string.Empty;
            var memo = FirstTag(block, "MEMO") ?? string.Empty;
            var label = string.Join(" — ", new[] { name, memo }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
            if (string.IsNullOrEmpty(label))
                label = FirstTag(block, "TRNTYPE") ?? "Opération bancaire";

            lines.Add(new ImportBankStatementLineRequest
            {
                TransactionDate = date.Value,
                ValueDate = valueDate,
                Reference = (FirstTag(block, "FITID") ?? FirstTag(block, "CHECKNUM") ?? string.Empty).Trim(),
                Description = label,
                // OFX : TRNAMT positif = encaissement (crédit), négatif = décaissement (débit).
                Amount = Math.Abs(amount.Value),
                IsDebit = amount.Value < 0
            });
        }

        if (lines.Count == 0 && issues.All(x => !x.IsBlocking))
            issues.Add(Blocking("fichier", "Aucune opération exploitable dans le fichier OFX."));

        return new BankFileParseResult(lines, issues, detectedAccount, detectedRib,
            OpeningBalance: null, ClosingBalance: closingBalance, StatementDate: lines.Count > 0 ? lines.Max(l => l.TransactionDate) : null);
    }

    /// <summary>Valeur d'une balise, lue jusqu'au prochain « &lt; » ou saut de ligne (SGML + XML).</summary>
    private static string? FirstTag(string source, string tag)
    {
        var m = Regex.Match(source, $"<{tag}>\\s*([^<\r\n]*)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static DateTime? ParseOfxDate(string? value)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length < 8)
            return null;
        var ymd = value[..8];
        return DateTime.TryParseExact(ymd, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.Date : null;
    }

    private static decimal? ParseAmount(string? value) =>
        TabularFileParsing.TryParseAmount(value ?? string.Empty, out var amount) && !string.IsNullOrWhiteSpace(value)
            ? amount : (decimal?)null;

    private static BankFileParseResult Empty(string message) =>
        new(Array.Empty<ImportBankStatementLineRequest>(), new[] { Blocking("fichier", message) });

    private static ImportIssueDto Blocking(string reference, string message) =>
        new() { Ref = reference, Message = message, IsBlocking = true };

    private static ImportIssueDto NonBlocking(string reference, string message) =>
        new() { Ref = reference, Message = message, IsBlocking = false };
}
