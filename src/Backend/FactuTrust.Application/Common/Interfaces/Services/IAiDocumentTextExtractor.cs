namespace FactuTrust.Application.Common.Interfaces.Services;

public sealed record AiDocumentExtractedPage
{
    public int PageIndex { get; init; }
    public string Text { get; init; } = "";
    public string? ImageBase64 { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public bool OcrApplied { get; init; }
}

public sealed record AiDocumentExtractionResult
{
    public bool Success { get; init; }
    public string Text { get; init; } = "";
    public bool Truncated { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>Format détecté ("pdf" | "image" | "docx" | "xlsx" | "csv" | "txt").</summary>
    public string Format { get; init; } = "";
    public bool OcrApplied { get; init; }
    public int PageCount { get; init; }
    public IReadOnlyList<AiDocumentExtractedPage> Pages { get; init; } = Array.Empty<AiDocumentExtractedPage>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public static AiDocumentExtractionResult Ok(string text, bool truncated) =>
        new() { Success = true, Text = text, Truncated = truncated };

    public static AiDocumentExtractionResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}

public sealed record AiDocumentExtractOptions
{
    /// <summary>Si vrai, rend les pages PDF/image en PNG base64 dans la réponse (pour modèles vision).</summary>
    public bool RenderPagesAsImages { get; init; }
    /// <summary>DPI de rendu (par défaut 200, max 300).</summary>
    public int RenderDpi { get; init; } = 200;

    public static AiDocumentExtractOptions Default { get; } = new();
}

public interface IAiDocumentTextExtractor
{
    /// <summary>Extracts plain text from supported formats (text/plain, PDF, images, DOCX, XLSX).</summary>
    Task<AiDocumentExtractionResult> ExtractAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Surcharge étendue avec options (rendu des pages en PNG base64 pour les modèles vision).
    /// </summary>
    Task<AiDocumentExtractionResult> ExtractAsync(
        Stream stream,
        string fileName,
        string contentType,
        AiDocumentExtractOptions options,
        CancellationToken cancellationToken = default);
}
