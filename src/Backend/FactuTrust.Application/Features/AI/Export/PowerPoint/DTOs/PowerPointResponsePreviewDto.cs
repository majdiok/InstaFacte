namespace FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;

/// <summary>
/// Lightweight preview of structured content detected in an assistant response before export.
/// </summary>
public sealed class PowerPointResponsePreviewDto
{
    public Guid ConversationId { get; init; }
    public Guid MessageId { get; init; }
    public string Title { get; init; } = string.Empty;
    public int KpiCount { get; init; }
    public int TableCount { get; init; }
    public int ChartCount { get; init; }
    public int SectionCount { get; init; }
    public bool HasText { get; init; }
    public IReadOnlyList<string> SlideOutline { get; init; } = Array.Empty<string>();
}
