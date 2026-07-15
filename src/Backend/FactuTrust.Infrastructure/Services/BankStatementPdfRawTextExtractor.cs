using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Extraction texte pour relevés BIAT texte natif : en-tête/footer via page.Text,
/// opérations reconstruites par colonnes (débit ~421, crédit ~533).
/// </summary>
public static class BankStatementPdfRawTextExtractor
{
    private const double RowBucketSize = 4.0;
    private const double DateColumnMaxX = 45;
    private const double DescriptionMinX = 45;
    private const double DescriptionMaxX = 168;
    private const double ReferenceMinX = 168;
    private const double ReferenceMaxX = 280;
    private const double ValueDateMinX = 280;
    private const double ValueDateMaxX = 390;
    private const double DebitAmountMinX = 390;
    private const double DebitAmountMaxX = 495;
    private const double CreditAmountMinX = 495;

    public sealed record RawPdfTextResult(bool Success, string Text, int PageCount, string? ErrorMessage = null);

    public static RawPdfTextResult Extract(byte[] content)
    {
        if (content is null || content.Length == 0)
            return new RawPdfTextResult(false, string.Empty, 0, "Fichier vide.");

        try
        {
            using var document = PdfDocument.Open(content);
            var pageCount = document.NumberOfPages;
            if (pageCount == 0)
                return new RawPdfTextResult(false, string.Empty, 0, "PDF sans page.");

            var pages = document.GetPages().ToList();
            var sb = new StringBuilder();

            sb.Append(ExtractHeaderSnippet(pages[0].Text ?? string.Empty));

            foreach (var page in pages)
            {
                foreach (var line in ExtractLayoutOperationLines(page))
                {
                    sb.Append('\n');
                    sb.Append(line);
                }
            }

            var footer = ExtractFooterSnippet(pages[^1].Text ?? string.Empty);
            if (!string.IsNullOrEmpty(footer))
            {
                sb.Append('\n');
                sb.Append(footer);
            }

            return new RawPdfTextResult(true, sb.ToString(), pageCount);
        }
        catch (Exception ex)
        {
            return new RawPdfTextResult(false, string.Empty, 0, ex.Message);
        }
    }

    /// <summary>Fallback page.Text concaténé (tests unitaires sur petits échantillons).</summary>
    public static RawPdfTextResult ExtractConcatenated(byte[] content)
    {
        if (content is null || content.Length == 0)
            return new RawPdfTextResult(false, string.Empty, 0, "Fichier vide.");

        try
        {
            using var document = PdfDocument.Open(content);
            var pageCount = document.NumberOfPages;
            if (pageCount == 0)
                return new RawPdfTextResult(false, string.Empty, 0, "PDF sans page.");

            var text = string.Join('\n', document.GetPages().Select(p => p.Text ?? string.Empty));
            return new RawPdfTextResult(true, text, pageCount);
        }
        catch (Exception ex)
        {
            return new RawPdfTextResult(false, string.Empty, 0, ex.Message);
        }
    }

    private static string ExtractHeaderSnippet(string pageText)
    {
        var headerBlock = Regex.Match(pageText,
            @"RIB\s*:.*?SOLDE\s+AU\s+\d{2}\s+\d{2}\s+\d{4}\s*[\d.,]+",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (headerBlock.Success)
            return headerBlock.Value.Trim();

        var parts = new List<string>();

        var rib = Regex.Match(pageText,
            @"RIB\s*:\s*([\d\s]+?)(\d{2})(STE[A-Z\s].*?)(?=RUE|SOLDE|\d{2}\s+\d{2}[A-Z]|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (rib.Success)
            parts.Add(rib.Value.Trim());

        var cityDate = Regex.Match(pageText,
            @"(?:MONASTIR|TUNIS|SFAX|SOUSSE)\s*\d{2}\s+\d{2}\s+20\d{2}",
            RegexOptions.IgnoreCase);
        if (cityDate.Success)
            parts.Add(cityDate.Value.Trim());

        var opening = Regex.Match(pageText,
            @"SOLDE\s+AU\s+\d{2}\s+\d{2}\s+\d{4}\s*[\d.,]+",
            RegexOptions.IgnoreCase);
        if (opening.Success)
            parts.Add(opening.Value.Trim());

        return string.Join('\n', parts);
    }

    private static string? ExtractFooterSnippet(string pageText)
    {
        var matches = Regex.Matches(pageText,
            @"DINAR\s+TUNISIEN\s*(?<closing>\d{1,3}(?:\.\d{3})*,\d{3})(?<debits>\d{1,3}(?:\.\d{3})*,\d{3})(?<credits>\d{1,3}(?:\.\d{3})*,\d{3})",
            RegexOptions.IgnoreCase);
        return matches.Count > 0 ? matches[^1].Value.Trim() : null;
    }

    private static IEnumerable<string> ExtractLayoutOperationLines(Page page)
    {
        var words = page.GetWords().ToList();
        if (words.Count == 0)
            yield break;

        var rows = words
            .GroupBy(w => Math.Round(w.BoundingBox.Top / RowBucketSize) * RowBucketSize)
            .OrderByDescending(g => g.Key);

        foreach (var row in rows)
        {
            var line = TryBuildLayoutLine(row.ToList());
            if (line is not null)
                yield return line;
        }
    }

    private static string? TryBuildLayoutLine(IReadOnlyList<Word> rowWords)
    {
        var ordered = rowWords.OrderBy(w => w.BoundingBox.Left).ToList();
        var dateWords = ordered.Where(w => w.BoundingBox.Left < DateColumnMaxX).ToList();
        if (dateWords.Count < 2)
            return null;
        if (!int.TryParse(dateWords[0].Text, out var day) || day is < 1 or > 31)
            return null;
        if (!int.TryParse(dateWords[1].Text, out var month) || month is < 1 or > 12)
            return null;

        var valueDateWord = ordered
            .FirstOrDefault(w => w.BoundingBox.Left >= ValueDateMinX && w.BoundingBox.Left < ValueDateMaxX &&
                                 w.Text.Length == 8 && w.Text.All(char.IsDigit));
        if (valueDateWord is null)
            return null;

        var preValueDate = ordered
            .Where(w => w.BoundingBox.Left >= DescriptionMinX && w.BoundingBox.Left < valueDateWord.BoundingBox.Left)
            .ToList();
        if (preValueDate.Count < 2)
            return null;

        var reference = preValueDate[^1].Text;
        if (reference.Length < 5 || !reference.Any(char.IsLetterOrDigit))
            return null;

        var description = string.Join(' ', preValueDate.Take(preValueDate.Count - 1).Select(w => w.Text)).Trim();
        if (string.IsNullOrEmpty(description) || !IsOperationDescription(description))
            return null;

        var valueDate = valueDateWord.Text;

        var debitAmount = ordered
            .Where(w => w.BoundingBox.Left >= DebitAmountMinX && w.BoundingBox.Left < DebitAmountMaxX)
            .OrderBy(w => w.BoundingBox.Left)
            .Select(w => w.Text)
            .FirstOrDefault(t => TunisianBankAmountParsing.TryParseStrictAmount(t, out _));

        var creditAmount = ordered
            .Where(w => w.BoundingBox.Left >= CreditAmountMinX)
            .OrderBy(w => w.BoundingBox.Left)
            .Select(w => w.Text)
            .FirstOrDefault(t => TunisianBankAmountParsing.TryParseStrictAmount(t, out _));

        string? amountToken = null;
        char side = 'D';
        if (creditAmount is not null)
        {
            amountToken = creditAmount;
            side = 'C';
        }
        else if (debitAmount is not null)
        {
            amountToken = debitAmount;
            side = 'D';
        }

        if (amountToken is null)
            return null;

        return $"{day:D2} {month:D2} {description} {reference} {valueDate} {side}:{amountToken}";
    }

    private static bool IsOperationDescription(string description)
    {
        var upper = description.ToUpperInvariant();
        string[] keywords =
        {
            "VIREMENT", "PAIEMENT", "REGLEMENT", "RÈGLEMENT", "ENCAISSEMENT", "COM ET", "COM TVA",
            "COMMISSION", "PRELEVEMENT", "PRÉLEVEMENT", "BQ A", "BLOCAGE", "DEBLOCAGE", "DÉBLOCAGE",
            "REDRESSEMENT", "VERSEMENT", "DEBIT", "ENG/", "COM Q"
        };

        return keywords.Any(k => upper.Contains(k, StringComparison.Ordinal));
    }
}
