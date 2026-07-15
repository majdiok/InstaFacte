namespace FactuTrust.Application.Features.AI.Export.PowerPoint.Services;

/// <summary>
/// In-memory result of a PowerPoint generation. The handler is responsible for either
/// streaming the content directly to the HTTP response or persisting it via
/// <c>IExportStorageService</c> for asynchronous download.
/// </summary>
public sealed record PowerPointGenerationResult
{
    /// <summary>Raw .pptx bytes. Owned by the caller; do not keep references after disposal.</summary>
    public byte[] Content { get; init; } = Array.Empty<byte>();

    public string FileName { get; init; } = "presentation.pptx";

    public int SlideCount { get; init; }

    public long SizeBytes => Content?.LongLength ?? 0L;

    public TimeSpan Duration { get; init; }

    /// <summary>Conversation IDs effectively used (post-validation). Used for audit logging.</summary>
    public IReadOnlyList<Guid> ConversationIds { get; init; } = Array.Empty<Guid>();

    /// <summary>Message IDs effectively included. Used for audit logging.</summary>
    public IReadOnlyList<Guid> MessageIds { get; init; } = Array.Empty<Guid>();
}
