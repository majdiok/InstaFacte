namespace FactuTrust.Application.Features.AI.Export.PowerPoint.Models;

/// <summary>
/// Slide aspect ratio. 16:9 is the modern default; 4:3 is supported for legacy projectors / printing.
/// </summary>
public enum SlideOrientation
{
    Widescreen16x9 = 0,
    Standard4x3 = 1
}
