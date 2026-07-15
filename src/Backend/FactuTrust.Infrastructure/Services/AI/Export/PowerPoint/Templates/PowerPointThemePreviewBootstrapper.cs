using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Templates;

/// <summary>Writes SVG preview thumbnails for hybrid themes into wwwroot on startup.</summary>
public sealed class PowerPointThemePreviewBootstrapper : IHostedService
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<PowerPointThemePreviewBootstrapper> _logger;

    public PowerPointThemePreviewBootstrapper(
        IHostEnvironment environment,
        ILogger<PowerPointThemePreviewBootstrapper> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var definition in PowerPointThemeLibrary.AllDefinitions.Where(d => d.Engine == Application.Features.AI.Export.PowerPoint.Models.PowerPointThemeEngine.Hybrid))
        {
            try
            {
                TemplatePreviewSvgWriter.EnsurePreview(_environment.ContentRootPath, definition);
                TemplateAssetWriter.EnsureBaseTemplates(_environment.ContentRootPath, definition);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write preview for theme {Theme}", definition.Template);
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal static class TemplatePreviewSvgWriter
{
    internal const int PreviewSvgVersion = 3;

    internal static void EnsurePreview(string contentRoot, PowerPointThemeDefinition definition)
    {
        var key = definition.BaseTemplateKey ?? definition.Template.ToString();
        var dir = Path.Combine(contentRoot, "wwwroot", "assets", "powerpoint", "themes", key);
        Directory.CreateDirectory(dir);

        var svg16 = Path.Combine(dir, "preview-16x9.svg");
        var versionPath = Path.Combine(dir, ".preview-version");
        if (ShouldRegeneratePreview(svg16, versionPath))
        {
            File.WriteAllText(svg16, BuildSvg(definition, 640, 360));
            File.WriteAllText(versionPath, PreviewSvgVersion.ToString());
        }

        var svg43 = Path.Combine(dir, "preview-4x3.svg");
        var versionPath43 = Path.Combine(dir, ".preview-version-4x3");
        if (ShouldRegeneratePreview(svg43, versionPath43))
        {
            File.WriteAllText(svg43, BuildSvg(definition, 640, 480));
            File.WriteAllText(versionPath43, PreviewSvgVersion.ToString());
        }

        var licensePath = Path.Combine(dir, "license.json");
        if (!File.Exists(licensePath))
        {
            File.WriteAllText(licensePath, $$"""
            {
              "themeId": {{(int)definition.Template}},
              "key": "{{key}}",
              "source": "Internal",
              "license": "Proprietary-FactuTrust",
              "attributionText": null,
              "assets": [],
              "dateAudit": "{{DateTime.UtcNow:yyyy-MM-dd}}",
              "reviewer": "FactuTrust-Engineering"
            }
            """);
        }
    }

    private static bool ShouldRegeneratePreview(string svgPath, string versionPath)
    {
        if (!File.Exists(svgPath))
            return true;

        if (File.ReadAllText(svgPath).Contains("##", StringComparison.Ordinal))
            return true;

        if (!File.Exists(versionPath))
            return true;

        return !int.TryParse(File.ReadAllText(versionPath).Trim(), out var version) || version < PreviewSvgVersion;
    }

    internal static string NormalizeSvgHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return "#000000";

        return $"#{hex.Trim().TrimStart('#')}";
    }

    private static string BuildSvg(PowerPointThemeDefinition definition, int width, int height)
    {
        var bg = NormalizeSvgHex(definition.IsDark ? definition.Colors.BackgroundHex : definition.Colors.SurfaceHex);
        var accent = NormalizeSvgHex(definition.Colors.AccentHex);
        var primary = NormalizeSvgHex(definition.IsDark ? "FFFFFF" : definition.Colors.PrimaryHex);
        var body = NormalizeSvgHex(definition.Colors.OnSurfaceHex);
        var accentSoft = NormalizeSvgHex(definition.Colors.AccentSoftHex);
        var title = System.Net.WebUtility.HtmlEncode(definition.Name);

        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">
              <rect width="100%" height="100%" fill="{bg}"/>
              <rect x="0" y="0" width="{width}" height="24" fill="{accent}"/>
              <rect x="{width * 0.65}" y="0" width="{width * 0.35}" height="{height}" fill="{accentSoft}" opacity="0.55"/>
              <text x="24" y="72" font-family="{definition.Fonts.TitleFamily}, Inter, sans-serif" font-size="28" font-weight="700" fill="{primary}">Titre</text>
              <text x="24" y="108" font-family="{definition.Fonts.BodyFamily}, Inter, sans-serif" font-size="16" fill="{body}">
                Corps et <tspan fill="{accent}" font-weight="600">lien</tspan>
              </text>
              <text x="24" y="{height - 16}" font-family="Inter, sans-serif" font-size="12" fill="{body}" opacity="0.7">{title}</text>
            </svg>
            """;
    }
}

internal static class TemplateAssetWriter
{
    internal static void EnsureBaseTemplates(string contentRoot, PowerPointThemeDefinition definition)
    {
        var key = definition.BaseTemplateKey ?? definition.Template.ToString();
        var dir = Path.Combine(contentRoot, "wwwroot", "assets", "powerpoint", "themes", key);
        Directory.CreateDirectory(dir);

        var path16 = Path.Combine(dir, "base-16x9.pptx");
        if (!File.Exists(path16))
            File.WriteAllBytes(path16, PowerPointBaseTemplateBuilder.Build(definition, SlideOrientation.Widescreen16x9));

        var path43 = Path.Combine(dir, "base-4x3.pptx");
        if (!File.Exists(path43))
            File.WriteAllBytes(path43, PowerPointBaseTemplateBuilder.Build(definition, SlideOrientation.Standard4x3));
    }
}
