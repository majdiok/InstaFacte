using System.Globalization;
using System.Text.RegularExpressions;
using FactuTrust.Application.DTOs;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Parse le texte extrait d'un relevé bancaire tunisien (format BIAT texte natif).
/// Gère le texte concaténé par page (PdfPig) et le format lignes espacées.
/// </summary>
public sealed partial class TunisianBankStatementTextParser
{
  private const int MaxLines = 50_000;

  public sealed record ParsedHeader(
    string? RibDigits,
    string? BankCode,
    string? HolderName,
    DateTime? StatementDate,
    DateTime? OpeningBalanceDate,
    decimal? OpeningBalance,
    decimal? ClosingBalance,
    decimal? TotalDebits,
    decimal? TotalCredits,
    int PageCount);

  public (ParsedHeader Header, IReadOnlyList<ImportBankStatementLineRequest> Lines, IReadOnlyList<ImportIssueDto> Issues)
    Parse(string text)
  {
    var issues = new List<ImportIssueDto>();
    var lines = new List<ImportBankStatementLineRequest>();

    if (string.IsNullOrWhiteSpace(text))
    {
      issues.Add(Issue("fichier", "Texte du relevé vide.", blocking: true));
      return (EmptyHeader(), lines, issues);
    }

    var normalized = PrepareText(text);
    var pageCount = PageMarkerRegex().Matches(normalized).Count;
    if (pageCount == 0 && RibHeaderRegex().IsMatch(normalized))
      pageCount = RibHeaderRegex().Matches(normalized).Count;

    var header = ExtractHeader(normalized);
    var statementYear = header.StatementDate?.Year ?? header.OpeningBalanceDate?.Year ?? DateTime.UtcNow.Year;
    var periodMonth = header.StatementDate?.Month ?? header.OpeningBalanceDate?.Month;

    var operationMatches = OperationRowRegex().Matches(normalized);
    var rejected = 0;
    var matchIndex = 0;

    foreach (Match match in operationMatches)
    {
      if (lines.Count >= MaxLines)
      {
        issues.Add(Issue("fichier", $"Relevé tronqué : plus de {MaxLines} lignes.", blocking: true));
        break;
      }

      matchIndex++;
      if (TryParseOperationMatch(match, statementYear, periodMonth, out var tx))
        lines.Add(tx);
      else
      {
        rejected++;
        var snippet = match.Value.Length > 50 ? match.Value[..50] + "…" : match.Value;
        issues.Add(Issue($"ligne-{matchIndex}", $"Opération non parsée : {snippet}", blocking: false));
      }
    }

    if (lines.Count == 0)
    {
      foreach (Match match in SpacedOperationRowRegex().Matches(normalized))
      {
        if (TryParseOperationMatch(match, statementYear, periodMonth, out var tx, spacedFormat: true))
          lines.Add(tx);
      }
    }

    if (rejected > 0 && operationMatches.Count > 0)
    {
      var pct = rejected * 100.0 / operationMatches.Count;
      if (pct > 5)
      {
        issues.Add(Issue("fichier",
          $"{rejected} opération(s) non parsée(s) ({pct:F0} % du relevé).",
          blocking: false));
      }
    }

    if (lines.Count == 0)
      issues.Add(Issue("fichier", "Aucune opération bancaire détectée dans le relevé.", blocking: true));

    if (string.IsNullOrEmpty(header.RibDigits))
      issues.Add(Issue("rib", "RIB non détecté dans le relevé.", blocking: false));

    return (header with { PageCount = Math.Max(pageCount, 1) }, lines, issues);
  }

  private static string PrepareText(string text)
  {
    var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
    normalized = InsertOperationBoundariesRegex().Replace(normalized, "\n$1 $2$3");
    return normalized;
  }

  private static ParsedHeader ExtractHeader(string text)
  {
    string? rib = null;
    string? bankCode = null;
    string? holder = null;
    DateTime? statementDate = null;
    DateTime? openingDate = null;
    decimal? opening = null;
    decimal? closing = null;
    decimal? totalDebits = null;
    decimal? totalCredits = null;

    var ribMatch = RibHeaderRegex().Match(text);
    if (!ribMatch.Success)
      ribMatch = RibHeaderSpacedRegex().Match(text);
    if (ribMatch.Success)
    {
      var ribPart = NormalizeDigits(ribMatch.Groups[1].Value);
      var ribSuffix = ribMatch.Groups[2].Success ? ribMatch.Groups[2].Value : string.Empty;
      rib = ribPart + ribSuffix;
      if (rib.Length > 20)
        rib = rib[..20];
      if (rib.Length == 20 && rib.Length >= 2)
        bankCode = rib[..2];
      if (ribMatch.Groups.Count > 3 && ribMatch.Groups[3].Success)
        holder = ribMatch.Groups[3].Value.Trim();
      if (string.IsNullOrWhiteSpace(holder))
        holder = null;
    }

    var openMatch = OpeningBalanceRegex().Match(text);
    if (openMatch.Success &&
        TryParseDayMonthYear(openMatch.Groups[1].Value, openMatch.Groups[2].Value, openMatch.Groups[3].Value, out var od) &&
        TunisianBankAmountParsing.TryParseStrictAmount(openMatch.Groups[4].Value, out var ob))
    {
      openingDate = od;
      opening = ob;
    }

    statementDate = ExtractStatementDate(text);

    var footerMatches = FooterTotalsRegex().Matches(text);
    if (footerMatches.Count > 0)
    {
      var footer = footerMatches[^1];
      if (TunisianBankAmountParsing.TryParseStrictAmount(footer.Groups["closing"].Value, out var c))
        closing = c;
      if (TunisianBankAmountParsing.TryParseStrictAmount(footer.Groups["debits"].Value, out var d))
        totalDebits = d;
      if (TunisianBankAmountParsing.TryParseStrictAmount(footer.Groups["credits"].Value, out var cr))
        totalCredits = cr;
    }

    return new ParsedHeader(rib, bankCode, holder, statementDate, openingDate, opening, closing, totalDebits, totalCredits, 1);
  }

  private static DateTime? ExtractStatementDate(string text)
  {
    var cityMatch = CityStatementDateRegex().Match(text);
    if (cityMatch.Success &&
        TryParseValidatedDayMonthYear(cityMatch.Groups[1].Value, cityMatch.Groups[2].Value, cityMatch.Groups[3].Value, out var cityDate))
      return cityDate;

    var beforeSoldeMatch = BeforeSoldeDateRegex().Match(text);
    if (beforeSoldeMatch.Success &&
        TryParseValidatedDayMonthYear(beforeSoldeMatch.Groups[1].Value, beforeSoldeMatch.Groups[2].Value, beforeSoldeMatch.Groups[3].Value, out var soldeDate))
      return soldeDate;

    foreach (Match m in ValidYearDateRegex().Matches(text))
    {
      if (TryParseValidatedDayMonthYear(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, out var d))
        return d;
    }

    return null;
  }

  private static bool TryParseOperationMatch(
    Match match, int defaultYear, int? periodMonth, out ImportBankStatementLineRequest line, bool spacedFormat = false)
  {
    line = null!;
    if (!int.TryParse(match.Groups[1].Value, out var day) || day is < 1 or > 31)
      return false;
    if (!int.TryParse(match.Groups[2].Value, out var month) || month is < 1 or > 12)
      return false;

    var description = match.Groups[3].Value.Trim();
    string reference;
    string valueDateToken;
    string amountToken;
    bool? explicitDebit = null;

    if (spacedFormat)
    {
      reference = match.Groups[4].Value;
      valueDateToken = match.Groups[5].Value;
      amountToken = match.Groups[6].Value;
    }
    else
    {
      var tail = match.Groups[4].Value.Trim();
      if (!TryParseOperationTail(tail, out reference, out valueDateToken, out amountToken, out explicitDebit))
        return false;
    }

    if (!TunisianBankAmountParsing.TryParseStrictAmount(amountToken, out var amount))
      return false;
    if (!TunisianBankAmountParsing.IsPlausibleOperationAmount(amount))
      return false;

    var year = InferYear(day, month, defaultYear, periodMonth);
    var txDate = new DateTime(year, month, day);
    var valueDate = ParseValueDate(valueDateToken, year, txDate);

    line = new ImportBankStatementLineRequest
    {
      TransactionDate = txDate,
      ValueDate = valueDate,
      Reference = reference,
      Description = description,
      Amount = amount,
      IsDebit = explicitDebit ?? BankOperationClassifier.IsDebit(description)
    };
    return true;
  }

  private static bool TryParseOperationTail(
    string tail, out string reference, out string valueDateToken, out string amountToken, out bool? explicitDebit)
  {
    reference = valueDateToken = amountToken = string.Empty;
    explicitDebit = null;
    tail = tail.Trim();
    if (string.IsNullOrEmpty(tail))
      return false;

    var markedMatch = MarkedAmountTailRegex().Match(tail);
    if (markedMatch.Success)
    {
      reference = markedMatch.Groups[1].Value;
      valueDateToken = markedMatch.Groups[2].Value;
      amountToken = markedMatch.Groups[4].Value;
      explicitDebit = markedMatch.Groups[3].Value.Equals("D", StringComparison.OrdinalIgnoreCase);
      return TunisianBankAmountParsing.TryParseStrictAmount(amountToken, out _);
    }

    var spacedMatch = SpacedTailRegex().Match(tail);
    if (spacedMatch.Success)
    {
      reference = spacedMatch.Groups[1].Value;
      valueDateToken = spacedMatch.Groups[2].Value;
      amountToken = spacedMatch.Groups[3].Value;
      return TunisianBankAmountParsing.TryParseStrictAmount(amountToken, out _);
    }

    for (var start = tail.Length - 1; start >= 0; start--)
    {
      var candidate = tail[start..];
      if (!TunisianBankAmountParsing.TryParseStrictAmount(candidate, out var amount))
        continue;
      if (!TunisianBankAmountParsing.IsPlausibleOperationAmount(amount))
        continue;

      var digits = new string(tail[..start].Where(char.IsDigit).ToArray());
      if (digits.Length < 8)
        continue;

      var valueDate = digits[^8..];
      if (!IsValidValueDateDigits(valueDate))
        continue;

      amountToken = candidate;
      valueDateToken = valueDate;
      reference = digits.Length > 8 ? digits[..^8] : string.Empty;
      return true;
    }

    return false;
  }

  private static bool IsValidValueDateDigits(string token)
  {
    if (token.Length != 8)
      return false;
    if (!int.TryParse(token[..2], out var day) || day is < 1 or > 31)
      return false;
    if (!int.TryParse(token[2..4], out var month) || month is < 1 or > 12)
      return false;
    if (!int.TryParse(token[4..], out var year) || year is < 2000 or > 2100)
      return false;

    try
    {
      _ = new DateTime(year, month, day);
      return true;
    }
    catch
    {
      return false;
    }
  }

  private static int InferYear(int day, int month, int defaultYear, int? periodMonth)
  {
    if (periodMonth is null) return defaultYear;
    if (month > periodMonth.Value && month - periodMonth.Value > 6)
      return defaultYear - 1;
    return defaultYear;
  }

  private static DateTime ParseValueDate(string token, int fallbackYear, DateTime transactionDate)
  {
    if (token.Length == 8 &&
        int.TryParse(token[..2], out var d) &&
        int.TryParse(token[2..4], out var m) &&
        int.TryParse(token[4..], out var y) &&
        y is >= 2000 and <= 2100)
    {
      try { return new DateTime(y, m, d); }
      catch { /* fall through */ }
    }

    return transactionDate;
  }

  private static bool TryParseValidatedDayMonthYear(string dd, string mm, string yyyy, out DateTime date)
  {
    date = default;
    if (!int.TryParse(dd, out var d) || !int.TryParse(mm, out var m) || !int.TryParse(yyyy, out var y))
      return false;
    if (y is < 2000 or > 2100)
      return false;
    try { date = new DateTime(y, m, d); return true; }
    catch { return false; }
  }

  private static bool TryParseDayMonthYear(string dd, string mm, string yyyy, out DateTime date)
  {
    date = default;
    if (!int.TryParse(dd, out var d) || !int.TryParse(mm, out var m) || !int.TryParse(yyyy, out var y))
      return false;
    try { date = new DateTime(y, m, d); return true; }
    catch { return false; }
  }

  private static string NormalizeDigits(string value) =>
    new(value.Where(char.IsDigit).ToArray());

  private static ImportIssueDto Issue(string reference, string message, bool blocking) =>
    new() { Ref = reference, Message = message, IsBlocking = blocking };

  private static ParsedHeader EmptyHeader() =>
    new(null, null, null, null, null, null, null, null, null, 0);

  [GeneratedRegex(@"^--\s*\d+\s+of\s+\d+\s*--$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
  private static partial Regex PageMarkerRegex();

  [GeneratedRegex(@"RIB\s*:\s*([\d\s]+?)(\d{2})(STE[A-Z\s].*?)(?=RUE|SOLDE|\d{2}\s+\d{2}[A-Z]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
  private static partial Regex RibHeaderRegex();

  [GeneratedRegex(@"RIB\s*:\s*([\d\s]+?)(\d{2})\s+(STE[A-Z\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
  private static partial Regex RibHeaderSpacedRegex();

  [GeneratedRegex(@"(?:MONASTIR|TUNIS|SFAX|SOUSSE)\s*(\d{2})\s+(\d{2})\s+(20\d{2})", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
  private static partial Regex CityStatementDateRegex();

  [GeneratedRegex(@"(\d{2})\s+(\d{2})\s+(20\d{2})(?=\s*SOLDE)", RegexOptions.Compiled)]
  private static partial Regex BeforeSoldeDateRegex();

  [GeneratedRegex(@"(\d{2})\s+(\d{2})\s+(20\d{2})", RegexOptions.Compiled)]
  private static partial Regex ValidYearDateRegex();

  [GeneratedRegex(@"SOLDE\s+AU\s+(\d{2})\s+(\d{2})\s+(\d{4})\s*([\d.,]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
  private static partial Regex OpeningBalanceRegex();

  [GeneratedRegex(
    @"DINAR\s+TUNISIEN\s*(?<closing>\d{1,3}(?:\.\d{3})*,\d{3})(?<debits>\d{1,3}(?:\.\d{3})*,\d{3})(?<credits>\d{1,3}(?:\.\d{3})*,\d{3})",
    RegexOptions.IgnoreCase | RegexOptions.Compiled)]
  private static partial Regex FooterTotalsRegex();

  [GeneratedRegex(
    @"(\d{2}) (\d{2})\s*(VIREMENT TN MEME BQ|VIREMENT TN AUTRE BQ|VIREMENT TN AUTRE|VIREMENT ETRANGER REC|VIREMENT RECU|PAIEMENT EFFET|REGLEMENT CHEQUE|ENCAISSEMENT CHEQUE|ENCAISSEMENT EFFETTN|ENCAISSEMENT EFFET|COM ET TVA[^0-9\n]*|COM TVA[^0-9\n]*|COMMISSION[^0-9\n]*|PRELEVEMENT BANCAIRE|BQ A DISTANCE[^0-9\n]*|BLOCAGE|DEBLOCAGE|REDRESSEMENT|VERSEMENT ESPECES|DEBIT PAR CREATION[^0-9\n]*|ENG/SIGNATURE|COM\s+Q\d[^0-9\n]*)\s*([^\n]*)",
    RegexOptions.IgnoreCase | RegexOptions.Compiled)]
  private static partial Regex OperationRowRegex();

  [GeneratedRegex(
    @"(\d{2}) (\d{2})\s*(VIREMENT TN MEME BQ|VIREMENT TN AUTRE BQ|PAIEMENT EFFET|REGLEMENT CHEQUE|ENCAISSEMENT CHEQUE|COM ET TVA|COM TVA|COMMISSION|PRELEVEMENT BANCAIRE|VIREMENT TN AUTRE|VIREMENT RECU|VIREMENT ETRANGER REC)\s+(\d+)\s+(\d{8})\s+(" + TunisianBankAmountParsing.StrictAmountPattern + @")",
    RegexOptions.IgnoreCase | RegexOptions.Compiled)]
  private static partial Regex SpacedOperationRowRegex();

  [GeneratedRegex(
    @"(\d{2}) (\d{2})\s*(VIREMENT|PAIEMENT|REGLEMENT|ENCAISSEMENT|COM ET|COM TVA|COMMISSION|PRELEVEMENT|BQ A|BLOCAGE|DEBLOCAGE|REDRESSEMENT|VERSEMENT|DEBIT|ENG/|COM\s+Q\d)",
    RegexOptions.IgnoreCase | RegexOptions.Compiled)]
  private static partial Regex InsertOperationBoundariesRegex();

  [GeneratedRegex(@"^([A-Z0-9]+)\s+(\d{8})\s+([DC]):(" + TunisianBankAmountParsing.StrictAmountPattern + @")$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
  private static partial Regex MarkedAmountTailRegex();

  [GeneratedRegex(@"^([A-Z0-9]+)\s+(\d{8})\s+(" + TunisianBankAmountParsing.StrictAmountPattern + @")$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
  private static partial Regex SpacedTailRegex();
}
