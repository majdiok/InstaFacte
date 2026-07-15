using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;

/// <summary>
/// Pluggable visual theme applied to every slide builder. Implementations live in the same
/// folder (one per <see cref="PowerPointTemplate"/> value). Keeping the contract narrow makes
/// it trivial to add new themes without touching builders.
/// </summary>
public interface IPowerPointTheme
{
    PowerPointTemplate Template { get; }
    string Name { get; }
    string Description { get; }

    PowerPointThemeCategory Category { get; }
    bool IsDark { get; }
    int SortOrder { get; }
    string? PreviewGradientCss { get; }

    PowerPointThemeEngine Engine { get; }
    string? BaseTemplateKey { get; }
    CoverLayoutStyle CoverStyle { get; }
    ThemeAttribution? RequiredAttribution { get; }
    string? PreviewThumbnailPath { get; }

    ThemeColors Colors { get; }
    ThemeFonts Fonts { get; }

    /// <summary>Optional logo to embed on the cover and headers, as PNG bytes.</summary>
    byte[]? LogoOverlayPng { get; }
}

/// <summary>Hex color tokens used by builders. Hex strings without the leading <c>#</c> (PPTX format).</summary>
public sealed record ThemeColors
{
    public string PrimaryHex { get; init; } = "0F172A";       // slate-900
    public string SecondaryHex { get; init; } = "475569";     // slate-600
    public string AccentHex { get; init; } = "2563EB";        // blue-600
    public string AccentSoftHex { get; init; } = "DBEAFE";    // blue-100
    public string BackgroundHex { get; init; } = "FFFFFF";    // white
    public string SurfaceHex { get; init; } = "F8FAFC";       // slate-50
    public string OnSurfaceHex { get; init; } = "0F172A";     // slate-900
    public string MutedHex { get; init; } = "94A3B8";         // slate-400
    public string SuccessHex { get; init; } = "059669";       // emerald-600
    public string WarningHex { get; init; } = "D97706";       // amber-600
    public string DangerHex { get; init; } = "DC2626";        // red-600

    /// <summary>Palette used to color chart series (cycled).</summary>
    public IReadOnlyList<string> ChartSeriesHex { get; init; } = new[]
    {
        "2563EB", "059669", "D97706", "DC2626", "7C3AED", "0891B2", "BE185D", "65A30D"
    };
}

public sealed record ThemeFonts
{
    public string TitleFamily { get; init; } = "Inter";
    public string BodyFamily { get; init; } = "Inter";
    public string MonoFamily { get; init; } = "Consolas";
}
