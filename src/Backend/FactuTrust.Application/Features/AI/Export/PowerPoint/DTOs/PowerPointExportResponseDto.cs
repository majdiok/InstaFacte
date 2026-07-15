namespace FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;

/// <summary>
/// JSON envelope returned by <c>POST /api/ai/exports/powerpoint</c> when the file is too large to
/// stream inline. Clients use <c>DownloadUrl</c> with a short TTL (1 hour by default).
/// </summary>
public sealed record PowerPointExportResponseDto
{
    public Guid ExportId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public int SlideCount { get; init; }
    public DateTime GeneratedAt { get; init; }
    public DateTime ExpiresAt { get; init; }

    /// <summary>Signed, time-limited download URL (relative to the API base).</summary>
    public string DownloadUrl { get; init; } = string.Empty;
}
