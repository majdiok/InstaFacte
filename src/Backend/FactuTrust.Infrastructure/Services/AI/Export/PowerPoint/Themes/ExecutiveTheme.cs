using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;

/// <summary>
/// Premium minimalist theme for executive committees: charcoal + golden accent, abundant
/// whitespace, large titles, neutral palette. Optimised for printed handouts and projected
/// boardroom screens.
/// </summary>
public sealed class ExecutiveTheme : IPowerPointTheme
{
    private readonly byte[]? _logoPng;

    public ExecutiveTheme(byte[]? logoPng = null)
    {
        _logoPng = logoPng;
    }

    public PowerPointTemplate Template => PowerPointTemplate.Executive;
    public string Name => "Executive";
    public string Description => "Palette charbon et doré, généreuse en espaces blancs. Pensée pour les comités de direction.";
    public PowerPointThemeCategory Category => PowerPointThemeCategory.Premium;
    public bool IsDark => false;
    public int SortOrder => 2;
    public string? PreviewGradientCss => null;
    public PowerPointThemeEngine Engine => PowerPointThemeEngine.Legacy;
    public string? BaseTemplateKey => null;
    public CoverLayoutStyle CoverStyle => CoverLayoutStyle.ClassicBar;
    public ThemeAttribution? RequiredAttribution => null;
    public string? PreviewThumbnailPath => null;

    public ThemeColors Colors { get; } = new()
    {
        PrimaryHex = "111827",       // gray-900 (charcoal)
        SecondaryHex = "374151",
        AccentHex = "B45309",        // amber-700 (gold)
        AccentSoftHex = "FEF3C7",
        BackgroundHex = "FFFFFF",
        SurfaceHex = "F9FAFB",
        OnSurfaceHex = "111827",
        MutedHex = "6B7280",
        SuccessHex = "047857",
        WarningHex = "B45309",
        DangerHex = "991B1B",
        ChartSeriesHex = new[]
        {
            "111827", "B45309", "047857", "1E3A8A", "6B21A8", "0E7490", "9F1239", "3F6212"
        }
    };

    public ThemeFonts Fonts { get; } = new()
    {
        TitleFamily = "Inter",
        BodyFamily = "Inter",
        MonoFamily = "Consolas"
    };

    public byte[]? LogoOverlayPng => _logoPng;
}
