using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;

/// <summary>
/// Default theme: balanced, neutral-light, broadly applicable. Subtle FactuTrust branding.
/// </summary>
public sealed class StandardTheme : IPowerPointTheme
{
    private readonly byte[]? _logoPng;

    public StandardTheme(byte[]? logoPng = null)
    {
        _logoPng = logoPng;
    }

    public PowerPointTemplate Template => PowerPointTemplate.Standard;
    public string Name => "Standard";
    public string Description => "Mise en page polyvalente et neutre, équilibrée entre texte et visuel.";
    public PowerPointThemeCategory Category => PowerPointThemeCategory.Light;
    public bool IsDark => false;
    public int SortOrder => 0;
    public string? PreviewGradientCss => null;
    public PowerPointThemeEngine Engine => PowerPointThemeEngine.Legacy;
    public string? BaseTemplateKey => null;
    public CoverLayoutStyle CoverStyle => CoverLayoutStyle.ClassicBar;
    public ThemeAttribution? RequiredAttribution => null;
    public string? PreviewThumbnailPath => null;

    public ThemeColors Colors { get; } = new()
    {
        PrimaryHex = "0F172A",       // slate-900
        SecondaryHex = "475569",     // slate-600
        AccentHex = "2563EB",        // FactuTrust blue
        AccentSoftHex = "DBEAFE",
        BackgroundHex = "FFFFFF",
        SurfaceHex = "F8FAFC",
        OnSurfaceHex = "0F172A",
        MutedHex = "94A3B8",
        SuccessHex = "059669",
        WarningHex = "D97706",
        DangerHex = "DC2626"
    };

    public ThemeFonts Fonts { get; } = new()
    {
        TitleFamily = "Inter",
        BodyFamily = "Inter",
        MonoFamily = "Consolas"
    };

    public byte[]? LogoOverlayPng => _logoPng;
}
