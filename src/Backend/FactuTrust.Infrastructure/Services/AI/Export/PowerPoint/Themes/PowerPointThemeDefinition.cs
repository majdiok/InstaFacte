using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;

/// <summary>Immutable theme definition used by <see cref="PowerPointThemeLibrary"/>.</summary>
public sealed record PowerPointThemeDefinition(
    PowerPointTemplate Template,
    string Name,
    string Description,
    PowerPointThemeCategory Category,
    bool IsDark,
    int SortOrder,
    ThemeColors Colors,
    ThemeFonts Fonts,
    string? PreviewGradientCss = null,
    PowerPointThemeEngine Engine = PowerPointThemeEngine.Legacy,
    string? BaseTemplateKey = null,
    CoverLayoutStyle CoverStyle = CoverLayoutStyle.ClassicBar,
    ThemeAttribution? RequiredAttribution = null,
    string? PreviewThumbnailPath = null);
