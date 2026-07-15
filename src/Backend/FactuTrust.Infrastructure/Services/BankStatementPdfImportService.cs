using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.BankStatementImport;
using FactuTrust.Application.Features.AI;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Import PDF/image de relevé bancaire : extraction texte → parser BIAT → fallback LLM.
/// </summary>
public sealed class BankStatementPdfImportService : IBankStatementPdfImportService
{
    private readonly IAiDocumentTextExtractor _documentExtractor;
    private readonly ImportBankStatementFromFileHandler _llmHandler;
    private readonly BankAccountMatcher _bankMatcher;
    private readonly AccountingSettings _settings;
    private readonly ILogger<BankStatementPdfImportService> _logger;
    private readonly TunisianBankStatementTextParser _textParser = new();

    public BankStatementPdfImportService(
        IAiDocumentTextExtractor documentExtractor,
        ImportBankStatementFromFileHandler llmHandler,
        BankAccountMatcher bankMatcher,
        IOptions<AccountingSettings> settings,
        ILogger<BankStatementPdfImportService> logger)
    {
        _documentExtractor = documentExtractor;
        _llmHandler = llmHandler;
        _bankMatcher = bankMatcher;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Result<BankStatementFilePreviewDto>> PreviewAsync(
        byte[] content,
        string fileName,
        string contentType,
        BankStatementFileFormat format,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.BankStatementPdfImportEnabled)
            return Result.Failure<BankStatementFilePreviewDto>(Error.Validation("BankStatementImport",
                "L'import PDF de relevés bancaires est désactivé pour ce dossier."));

        if (content is null || content.Length == 0)
            return Result.Failure<BankStatementFilePreviewDto>(Error.Validation("File", "Fichier vide."));

        if (format == BankStatementFileFormat.Pdf)
        {
            var rawResult = BankStatementPdfRawTextExtractor.Extract(content);
            if (rawResult.Success &&
                BankStatementTextQuality.IsSufficient(rawResult.Text) &&
                TryParseDeterministic(rawResult.Text, rawResult.PageCount, ocrApplied: false, out var rawPreview))
            {
                return Result.Success(await BuildPreviewDtoAsync(
                    rawPreview.Lines, rawPreview.Issues, rawPreview.Rib, rawPreview.BankCode, rawPreview.BankName,
                    rawPreview.PeriodStart, rawPreview.PeriodEnd, rawPreview.Opening, rawPreview.Closing,
                    rawPreview.FooterTotalDebits, rawPreview.FooterTotalCredits,
                    BankStatementExtractionMethod.TextParser, rawPreview.PageCount, content, fileName, cancellationToken));
            }

            _logger.LogInformation(
                "Relevé PDF : extraction brute insuffisante ou 0 opération ({LineCount} lignes), bascule extracteur layout/OCR.",
                rawResult.Success ? CountParsedLines(rawResult.Text) : 0);
        }

        await using var stream = new MemoryStream(content);
        var extract = await _documentExtractor.ExtractAsync(stream, fileName, contentType, cancellationToken);
        if (!extract.Success || string.IsNullOrWhiteSpace(extract.Text))
        {
            var llmOnly = await _llmHandler.ExtractAsync(content, fileName, contentType, extract, cancellationToken);
            if (llmOnly.IsFailure)
                return Result.Failure<BankStatementFilePreviewDto>(llmOnly.Error);
            return await FinalizePreviewAsync(llmOnly.Value, content, fileName, BankStatementExtractionMethod.OcrLlm, extract.PageCount, cancellationToken);
        }

        if (BankStatementTextQuality.IsSufficient(extract.Text) || BankStatementImportOcrQuality.IsSufficient(extract.Text))
        {
            if (TryParseDeterministic(extract.Text, extract.PageCount, extract.OcrApplied, out var parsed))
            {
                var method = extract.OcrApplied
                    ? BankStatementExtractionMethod.OcrTextParser
                    : BankStatementExtractionMethod.TextParser;
                var preview = await BuildPreviewDtoAsync(
                    parsed.Lines, parsed.Issues, parsed.Rib, parsed.BankCode, parsed.BankName,
                    parsed.PeriodStart, parsed.PeriodEnd, parsed.Opening, parsed.Closing,
                    parsed.FooterTotalDebits, parsed.FooterTotalCredits,
                    method, parsed.PageCount, content, fileName, cancellationToken);
                return Result.Success(preview);
            }
        }

        _logger.LogInformation("Relevé bancaire : texte insuffisant pour parser déterministe, bascule LLM.");
        var llm = await _llmHandler.ExtractAsync(content, fileName, contentType, extract, cancellationToken);
        if (llm.IsFailure)
            return Result.Failure<BankStatementFilePreviewDto>(llm.Error);
        return await FinalizePreviewAsync(llm.Value, content, fileName, BankStatementExtractionMethod.OcrLlm, extract.PageCount, cancellationToken);
    }

    private sealed record DeterministicParseResult(
        IReadOnlyList<ImportBankStatementLineRequest> Lines,
        IReadOnlyList<ImportIssueDto> Issues,
        string? Rib,
        string? BankCode,
        string? BankName,
        DateTime? PeriodStart,
        DateTime? PeriodEnd,
        decimal? Opening,
        decimal? Closing,
        decimal? FooterTotalDebits,
        decimal? FooterTotalCredits,
        int PageCount);

    private bool TryParseDeterministic(string text, int pageCount, bool ocrApplied, out DeterministicParseResult result)
    {
        var parsed = _textParser.Parse(text);
        result = new DeterministicParseResult(
            parsed.Lines,
            parsed.Issues,
            parsed.Header.RibDigits,
            parsed.Header.BankCode,
            parsed.Header.HolderName,
            parsed.Header.OpeningBalanceDate?.AddDays(1)
                ?? (parsed.Lines.Count > 0 ? parsed.Lines.Min(l => l.TransactionDate) : null),
            parsed.Header.StatementDate,
            parsed.Header.OpeningBalance,
            parsed.Header.ClosingBalance,
            parsed.Header.TotalDebits,
            parsed.Header.TotalCredits,
            Math.Max(parsed.Header.PageCount, pageCount));

        return parsed.Lines.Count > 0;
    }

    private int CountParsedLines(string text) => _textParser.Parse(text).Lines.Count;

    internal async Task<BankStatementFilePreviewDto> BuildPreviewDtoAsync(
        IReadOnlyList<ImportBankStatementLineRequest> lines,
        IReadOnlyList<ImportIssueDto> issues,
        string? rib,
        string? bankCode,
        string? bankName,
        DateTime? periodStart,
        DateTime? periodEnd,
        decimal? opening,
        decimal? closing,
        decimal? footerTotalDebits,
        decimal? footerTotalCredits,
        BankStatementExtractionMethod method,
        int pageCount,
        byte[] content,
        string fileName,
        CancellationToken cancellationToken)
    {
        var allIssues = issues.ToList();
        var (match, matchIssues) = await _bankMatcher.MatchByRibAsync(rib, cancellationToken);
        allIssues.AddRange(matchIssues);

        if (closing is > 1_000_000_000m)
        {
            allIssues.Add(new ImportIssueDto
            {
                Ref = "fichier",
                Message = "Solde final aberrant détecté — vérifiez le format du relevé.",
                IsBlocking = true
            });
            closing = null;
        }

        var totalDebit = lines.Where(l => l.IsDebit).Sum(l => l.Amount);
        var totalCredit = lines.Where(l => !l.IsDebit).Sum(l => l.Amount);
        decimal? discrepancy = null;

        if (footerTotalDebits.HasValue && footerTotalDebits > 0)
        {
            var debitDelta = Math.Abs(totalDebit - footerTotalDebits.Value);
            var debitTolerance = Math.Max(1m, footerTotalDebits.Value * 0.001m);
            if (debitDelta > debitTolerance)
            {
                allIssues.Add(new ImportIssueDto
                {
                    Ref = "coherence",
                    Message = $"Total débits lignes {totalDebit:N3} vs relevé {footerTotalDebits:N3} (Δ {debitDelta:N3}).",
                    IsBlocking = false
                });
            }
        }

        if (footerTotalCredits.HasValue && footerTotalCredits > 0)
        {
            // BIAT : le total crédits du pied de page inclut le solde d'ouverture.
            var creditComparable = opening.HasValue ? opening.Value + totalCredit : totalCredit;
            var creditDelta = Math.Abs(creditComparable - footerTotalCredits.Value);
            var creditTolerance = Math.Max(1m, footerTotalCredits.Value * 0.001m);
            if (creditDelta > creditTolerance)
            {
                allIssues.Add(new ImportIssueDto
                {
                    Ref = "coherence",
                    Message = $"Total crédits lignes {totalCredit:N3} (+ solde initial {opening:N3}) vs relevé {footerTotalCredits:N3} (Δ {creditDelta:N3}).",
                    IsBlocking = false
                });
            }
        }

        if (opening.HasValue && closing.HasValue)
        {
            var computed = totalDebit - totalCredit - opening.Value;
            discrepancy = Math.Abs(computed - closing.Value);
            if (discrepancy > 1m)
            {
                allIssues.Add(new ImportIssueDto
                {
                    Ref = "coherence",
                    Message = $"Écart de solde : calculé {computed:N3} vs relevé {closing.Value:N3} (Δ {discrepancy:N3}).",
                    IsBlocking = false
                });
            }
        }

        var hasBlocking = allIssues.Any(i => i.IsBlocking);
        var confidence = method switch
        {
            BankStatementExtractionMethod.TextParser => 95,
            BankStatementExtractionMethod.OcrTextParser => 80,
            BankStatementExtractionMethod.OcrLlm => 65,
            _ => 50
        };

        return new BankStatementFilePreviewDto
        {
            TotalLines = lines.Count + allIssues.Count(i => i.Ref.StartsWith("ligne", StringComparison.OrdinalIgnoreCase)),
            ValidLines = lines.Count,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            CanImport = !hasBlocking && lines.Count > 0,
            Issues = allIssues,
            Lines = lines,
            DetectedBankCode = bankCode,
            DetectedRib = rib,
            MatchedBankAccountId = match.BankAccountId,
            MatchedChartOfAccountNumber = match.ChartOfAccountNumber,
            SuggestedBankName = match.BankName ?? bankName,
            SuggestedOpeningBalance = opening,
            SuggestedClosingBalance = closing,
            ExtractionMethod = method,
            ConfidenceScore = confidence,
            SourcePageCount = pageCount,
            BalanceDiscrepancy = discrepancy
        };
    }

    private async Task<Result<BankStatementFilePreviewDto>> FinalizePreviewAsync(
        LlmBankStatementExtraction llm,
        byte[] content,
        string fileName,
        BankStatementExtractionMethod method,
        int pageCount,
        CancellationToken cancellationToken)
    {
        var lines = MapLlmLines(llm);
        var issues = llm.Warnings.Select(w => new ImportIssueDto
        {
            Ref = "llm",
            Message = w,
            IsBlocking = false
        }).ToList();

        if (lines.Count == 0)
            issues.Add(new ImportIssueDto { Ref = "fichier", Message = "Aucune ligne extraite par l'IA.", IsBlocking = true });

        DateTime? periodStart = ToDateTime(InvoiceImportParsing.ParseDate(llm.PeriodStart));
        DateTime? periodEnd = ToDateTime(InvoiceImportParsing.ParseDate(llm.PeriodEnd ?? llm.StatementDate));
        if (!periodStart.HasValue && lines.Count > 0)
            periodStart = lines.Min(l => l.TransactionDate);
        if (!periodEnd.HasValue && lines.Count > 0)
            periodEnd = lines.Max(l => l.TransactionDate);

        var rib = string.IsNullOrWhiteSpace(llm.Rib) ? null : new string(llm.Rib.Where(char.IsDigit).ToArray());
        var preview = await BuildPreviewDtoAsync(
            lines, issues, rib, rib?.Length >= 2 ? rib[..2] : null, llm.BankName ?? llm.HolderName,
            periodStart, periodEnd, llm.OpeningBalance, llm.ClosingBalance,
            null, null,
            method, pageCount, content, fileName, cancellationToken);
        return Result.Success(preview);
    }

    private static List<ImportBankStatementLineRequest> MapLlmLines(LlmBankStatementExtraction llm)
    {
        var result = new List<ImportBankStatementLineRequest>();
        foreach (var l in llm.Lines)
        {
            if (string.IsNullOrWhiteSpace(l.Description) || l.Amount is null or <= 0)
                continue;
            var txDate = ToDateTime(InvoiceImportParsing.ParseDate(l.TransactionDate)) ?? DateTime.UtcNow.Date;
            result.Add(new ImportBankStatementLineRequest
            {
                TransactionDate = txDate,
                ValueDate = ToDateTime(InvoiceImportParsing.ParseDate(l.ValueDate)),
                Reference = l.Reference?.Trim() ?? string.Empty,
                Description = l.Description.Trim(),
                Amount = l.Amount.Value,
                IsDebit = l.IsDebit ?? BankOperationClassifier.IsDebit(l.Description)
            });
        }
        return result;
    }

    public static string ComputeFileHash(byte[] content)
    {
        var hash = SHA256.HashData(content);
        return Convert.ToHexString(hash);
    }

    private static DateTime? ToDateTime(DateOnly? d) =>
        d.HasValue ? d.Value.ToDateTime(TimeOnly.MinValue) : null;
}
