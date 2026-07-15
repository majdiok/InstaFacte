using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;

namespace FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;

/// <summary>
/// Public catalogue entry for a PowerPoint template, returned by
/// <c>GET /api/ai/exports/powerpoint/templates</c>. Lets the UI render rich preview cards
/// with name, description and thumbnail without hard-coding values client-side.
/// </summary>
public sealed record PowerPointTemplateInfoDto
{
    public PowerPointTemplate Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public PowerPointThemeCategory Category { get; init; }
    public bool IsDark { get; init; }
    public int SortOrder { get; init; }
    public string PrimaryColorHex { get; init; } = string.Empty;
    public string AccentColorHex { get; init; } = string.Empty;
    public string BackgroundColorHex { get; init; } = string.Empty;
    public string SurfaceColorHex { get; init; } = string.Empty;
    public string OnSurfaceColorHex { get; init; } = string.Empty;
    public string LinkColorHex { get; init; } = string.Empty;
    public string TitleFontFamily { get; init; } = string.Empty;
    public string BodyFontFamily { get; init; } = string.Empty;
    public string? PreviewGradientCss { get; init; }
    public PowerPointThemeEngine Engine { get; init; }
    public string? PreviewThumbnailUrl { get; init; }
    public string? PreviewThumbnailUrl4x3 { get; init; }
    public bool RequiresAttribution { get; init; }
    public string? AttributionSummary { get; init; }
    public CoverLayoutStyle CoverLayoutStyle { get; init; }
}