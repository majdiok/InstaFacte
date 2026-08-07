using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI;
using FactuTrust.Application.Features.Accounting.DocumentImport;
using FactuTrust.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.DocumentImport;

/// <summary>
/// Extraction d'une pièce comptable en deux passes :
/// <list type="number">
/// <item>parseur déterministe du gabarit InstaFact — instantané, gratuit, exact, et incapable de
/// rendre un résultat non réconcilié ;</item>
/// <item>extraction IA (OCR + LLM) pour toute autre pièce — typiquement une facture fournisseur
/// tierce, scannée ou photographiée.</item>
/// </list>
/// </summary>
public sealed class AccountingDocumentExtractor : IAccountingDocumentExtractor
{
    /// <summary>Code d'erreur et préfixe de log de ce chemin d'import.</summary>
    private const string ErrorCode = "AccountingDocumentImport";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly InstaFactInvoicePdfParser _nativeParser;
    private readonly IAiStructuredExtractionPipeline _pipeline;
    private readonly OllamaSettings _ollamaSettings;
    private readonly ILogger<AccountingDocumentExtractor> _logger;

    public AccountingDocumentExtractor(
        InstaFactInvoicePdfParser nativeParser,
        IAiStructuredExtractionPipeline pipeline,
        IOptions<OllamaSettings> ollamaSettings,
        ILogger<AccountingDocumentExtractor> logger)
    {
        _nativeParser = nativeParser;
        _pipeline = pipeline;
        _ollamaSettings = ollamaSettings.Value;
        _logger = logger;
    }

    public async Task<Result<AccountingDocumentExtractionDto>> ExtractAsync(
        AccountingDocumentExtractionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Le flux est lu une seule fois : le parseur natif a besoin des octets, et le pipeline IA
        // repart du même buffer si la 1re passe n'aboutit pas.
        var bytes = await ReadAllBytesAsync(request.FileStream, cancellationToken);
        if (bytes.Length == 0)
            return Result.Failure<AccountingDocumentExtractionDto>(
                Error.Validation(ErrorCode, "Le fichier est vide."));

        if (LooksLikePdf(bytes))
        {
            var native = _nativeParser.TryParse(bytes, request.FileName);
            if (native is not null)
            {
                _logger.LogInformation(
                    "[{Scope}] Pièce {FileName} lue par le parseur natif (aucun appel IA) : "
                    + "n°={Number} HT={Ht} TVA={Vat} TTC={Ttc}",
                    ErrorCode, request.FileName, native.DocumentNumber,
                    native.TotalHt, native.TotalVat, native.TotalTtc);
                return Result.Success(native);
            }
        }

        if (!request.AllowAiFallback)
        {
            return Result.Failure<AccountingDocumentExtractionDto>(Error.Validation(ErrorCode,
                "Cette pièce n'est pas au format des factures émises par InstaFact et son analyse "
                + "nécessite l'assistant IA, pour lequel vous n'avez pas d'autorisation. "
                + "Contactez votre administrateur ou saisissez l'écriture manuellement."));
        }

        return await ExtractWithAiAsync(bytes, request, cancellationToken);
    }

    private async Task<Result<AccountingDocumentExtractionDto>> ExtractWithAiAsync(
        byte[] bytes,
        AccountingDocumentExtractionRequest request,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream(bytes, writable: false);

        var pipelineResult = await _pipeline.RunAsync(
            new AiStructuredExtractionRequest
            {
                FileStream = buffer,
                FileName = request.FileName,
                ContentType = request.ContentType,
                SystemPrompt = _ollamaSettings.UseCompactImportPrompt
                    ? AccountingDocumentPrompt.CompactSystem
                    : AccountingDocumentPrompt.System,
                ModelOverride = request.ModelOverride,
                ErrorCode = ErrorCode
            },
            cancellationToken);

        if (pipelineResult.IsFailure)
            return Result.Failure<AccountingDocumentExtractionDto>(pipelineResult.Error);

        var outcome = pipelineResult.Value;
        var json = InvoiceImportParsing.ExtractFirstJsonObject(outcome.RawContent);

        LlmAccountingDocument? parsed = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                parsed = JsonSerializer.Deserialize<LlmAccountingDocument>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "[{Scope}] JSON renvoyé par le LLM non désérialisable.", ErrorCode);
            }
        }

        if (parsed is null)
        {
            return Result.Failure<AccountingDocumentExtractionDto>(Error.Validation(ErrorCode,
                "L'IA n'a pas pu produire des données exploitables à partir de ce fichier. "
                + "Réessayez ou saisissez l'écriture manuellement."));
        }

        var method = outcome.VisionUsed
            ? AccountingDocumentExtractionMethods.LlmVision
            : AccountingDocumentExtractionMethods.LlmText;

        var document = AccountingDocumentMapping.FromLlm(
            parsed, method, outcome.Extraction.OcrApplied, outcome.TextTruncated);

        _logger.LogInformation(
            "[{Scope}] Pièce {FileName} extraite par l'IA : méthode={Method} type={Type} n°={Number} "
            + "lignes={LineCount} tauxTva={RateCount} confiance={Confidence}",
            ErrorCode, request.FileName, method, document.DocumentType, document.DocumentNumber,
            document.Lines.Count, document.VatBreakdown.Count, document.Confidence);

        return Result.Success(document);
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream is MemoryStream ready)
            return ready.ToArray();

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private static bool LooksLikePdf(byte[] bytes) =>
        bytes.Length >= 5
        && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46; // "%PDF"
}
