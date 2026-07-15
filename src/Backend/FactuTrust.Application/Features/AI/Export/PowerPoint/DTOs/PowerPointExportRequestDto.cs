using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;

namespace FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;

/// <summary>
/// HTTP request body for <c>POST /api/ai/exports/powerpoint</c>.
/// Validated by <c>PowerPointExportRequestValidator</c> (max 50 responses, length limits, etc.).
/// </summary>
public sealed record PowerPointExportRequestDto
{
    /// <summary>Deck title shown on the cover slide (1-200 chars).</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Optional subtitle below the cover title (max 250 chars).</summary>
    public string? Subtitle { get; init; }

    /// <summary>Optional author/presenter name (max 200 chars). Defaults to the authenticated user.</summary>
    public string? AuthorName { get; init; }

    public PowerPointTemplate Template { get; init; } = PowerPointTemplate.Standard;

    public SlideOrientation Orientation { get; init; } = SlideOrientation.Widescreen16x9;

    public bool IncludeCoverSlide { get; init; } = true;
    public bool IncludeAgenda { get; init; } = true;
    public bool IncludeTableOfContents { get; init; }
    public bool IncludeSpeakerNotes { get; init; } = true;
    public bool IncludeSources { get; init; } = true;
    public bool IncludeAppendix { get; init; }

    /// <summary>Optional locale code used for numeric/date formatting (e.g. <c>fr-TN</c>). Defaults to <c>fr-TN</c>.</summary>
    public string? Locale { get; init; }

    /// <summary>Ordered list of assistant responses to include (1-50 items).</summary>
    public IReadOnlyList<ResponseSelectionDto> Responses { get; init; } = Array.Empty<ResponseSelectionDto>();
}
