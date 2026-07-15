using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;

namespace FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;

/// <summary>
/// Selection of a single assistant response (message) inside a conversation.
/// Multiple selections from different conversations are supported (cross-conversations).
/// </summary>
public sealed record ResponseSelectionDto
{
    public Guid ConversationId { get; init; }
    public Guid MessageId { get; init; }

    /// <summary>Optional custom slide title (overrides the auto-derived one). Max 200 chars.</summary>
    public string? CustomTitle { get; init; }

    /// <summary>
    /// When non-null, restrict the response to a subset of content blocks.
    /// <c>null</c> means include all available blocks (default).
    /// </summary>
    public SlideContentBlock? IncludeOnly { get; init; }
}
