namespace FactuTrust.Application.Features.AI.Export.PowerPoint.Models;

/// <summary>
/// Granular content block emitted from a single assistant response. Used by the API
/// to let clients include or exclude individual blocks (e.g. text only, tables only).
/// </summary>
[Flags]
public enum SlideContentBlock
{
    None = 0,
    Text = 1 << 0,
    KpiCards = 1 << 1,
    Tables = 1 << 2,
    Charts = 1 << 3,
    Sources = 1 << 4,
    All = Text | KpiCards | Tables | Charts | Sources
}
