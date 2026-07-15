namespace FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;

/// <summary>Compact audit row for the user-facing history view.</summary>
public sealed record PowerPointExportAuditDto
{
    public Guid Id { get; init; }
    public string Format { get; init; } = "pptx";
    public string Template { get; init; } = string.Empty;
    public string? Title { get; init; }
    public int ResponseCount { get; init; }
    public int SlideCount { get; init; }
    public long SizeBytes { get; init; }
    public DateTime GeneratedAt { get; init; }
    public bool Success { get; init; }
}
