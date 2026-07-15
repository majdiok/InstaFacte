using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;

/// <summary>
/// <see cref="IPowerPointTheme"/> implementation backed by a <see cref="PowerPointThemeDefinition"/>.
/// </summary>
public sealed class ConfigurablePowerPointTheme : IPowerPointTheme
{
    private readonly PowerPointThemeDefinition _definition;
    private readonly byte[]? _logoPng;

    public ConfigurablePowerPointTheme(PowerPointThemeDefinition definition, byte[]? logoPng = null)
    {
        _definition = definition;
        _logoPng = logoPng;
    }

    public PowerPointTemplate Template => _definition.Template;
    public string Name => _definition.Name;
    public string Description => _definition.Description;
    public PowerPointThemeCategory Category => _definition.Category;
    public bool IsDark => _definition.IsDark;
    public int SortOrder => _definition.SortOrder;
    public string? PreviewGradientCss => _definition.PreviewGradientCss;
    public PowerPointThemeEngine Engine => _definition.Engine;
    public string? BaseTemplateKey => _definition.BaseTemplateKey;
    public CoverLayoutStyle CoverStyle => _definition.CoverStyle;
    public ThemeAttribution? RequiredAttribution => _definition.RequiredAttribution;
    public string? PreviewThumbnailPath => _definition.PreviewThumbnailPath;
    public ThemeColors Colors => _definition.Colors;
    public ThemeFonts Fonts => _definition.Fonts;
    public byte[]? LogoOverlayPng => _logoPng;
}
