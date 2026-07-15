using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;

/// <summary>
/// Data-dense theme: prominent FactuTrust brand colour, vivid amber accent, compact tables,
/// chart-forward layouts. Optimised for management reviews and operational analyses.
/// </summary>
public sealed class AnalyseTheme : IPowerPointTheme
{
    private readonly byte[]? _logoPng;

    public AnalyseTheme(byte[]? logoPng = null)
    {
        _logoPng = logoPng;
    }

    public PowerPointTemplate Template => PowerPointTemplate.Analyse;
    public string Name => "Analyse";
    public string Description => "Données denses, graphiques mis en valeur, idéale pour les revues opérationnelles.";
    public PowerPointThemeCategory Category => PowerPointThemeCategory.Vibrant;
    public bool IsDark => false;
    public int SortOrder => 1;
    public string? PreviewGradientCss => null;
    public PowerPointThemeEngine Engine => PowerPointThemeEngine.Legacy;
    public string? BaseTemplateKey => null;
    public CoverLayoutStyle CoverStyle => CoverLayoutStyle.ClassicBar;
    public ThemeAttribution? RequiredAttribution => null;
    public string? PreviewThumbnailPath => null;

    public ThemeColors Colors { get; } = new()
    {
        PrimaryHex = "1E3A8A",       // FactuTrust deep blue
        SecondaryHex = "1E40AF",
        AccentHex = "F59E0B",        // amber-500 brand accent
        AccentSoftHex = "FEF3C7",
        BackgroundHex = "FFFFFF",
        SurfaceHex = "EFF6FF",       // blue-50
        OnSurfaceHex = "0F172A",
        MutedHex = "64748B",
        SuccessHex = "16A34A",
        WarningHex = "F97316",
        DangerHex = "DC2626",
        ChartSeriesHex = new[]
        {
            "2563EB", "F59E0B", "16A34A", "DC2626", "7C3AED", "0891B2", "BE185D", "65A30D"
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
