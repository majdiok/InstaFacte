using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;

/// <summary>
/// Slide canvas dimensions expressed in English Metric Units (EMU). 1 inch = 914 400 EMU.
/// Provides convenient subdivisions (margins, body box, footer band) so every builder lays out
/// content consistently.
/// </summary>
public sealed record SlideDimensions
{
    /// <summary>Total slide width in EMU.</summary>
    public long Width { get; init; }

    /// <summary>Total slide height in EMU.</summary>
    public long Height { get; init; }

    /// <summary>Uniform horizontal margin in EMU.</summary>
    public long MarginX { get; init; }

    /// <summary>Uniform vertical margin in EMU.</summary>
    public long MarginY { get; init; }

    /// <summary>Width available between left and right margins.</summary>
    public long ContentWidth => Width - (2 * MarginX);

    /// <summary>Height available between top and bottom margins.</summary>
    public long ContentHeight => Height - (2 * MarginY);

    /// <summary>Reserved height for the bottom footer (page numbers, date).</summary>
    public long FooterHeight { get; init; }

    /// <summary>Reserved height for the top header (title).</summary>
    public long HeaderHeight { get; init; }

    public long BodyTop => MarginY + HeaderHeight;
    public long BodyHeight => ContentHeight - HeaderHeight - FooterHeight;
    public long BodyLeft => MarginX;

    public static SlideDimensions For(SlideOrientation orientation) =>
        orientation switch
        {
            SlideOrientation.Standard4x3 => new SlideDimensions
            {
                Width = 9_144_000L,
                Height = 6_858_000L,
                MarginX = 457_200L, // 0.5"
                MarginY = 457_200L,
                HeaderHeight = 685_800L, // 0.75"
                FooterHeight = 304_800L  // 0.33"
            },
            _ => new SlideDimensions
            {
                // 16:9 widescreen — modern default
                Width = 12_192_000L,
                Height = 6_858_000L,
                MarginX = 457_200L,
                MarginY = 457_200L,
                HeaderHeight = 685_800L,
                FooterHeight = 304_800L
            }
        };
}
