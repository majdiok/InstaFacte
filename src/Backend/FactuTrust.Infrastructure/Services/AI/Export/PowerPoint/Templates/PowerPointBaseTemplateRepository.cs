using System.Collections.Concurrent;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Templates;

/// <summary>
/// Loads hybrid base .pptx bytes from disk or generates them on first access.
/// </summary>
public interface IPowerPointBaseTemplateRepository
{
    bool HasTemplate(IPowerPointTheme theme, SlideOrientation orientation);
    byte[] GetTemplateBytes(IPowerPointTheme theme, SlideOrientation orientation);
}

public sealed class PowerPointBaseTemplateRepository : IPowerPointBaseTemplateRepository
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<PowerPointBaseTemplateRepository> _logger;
    private readonly ConcurrentDictionary<string, byte[]> _cache = new();

    public PowerPointBaseTemplateRepository(
        IHostEnvironment environment,
        ILogger<PowerPointBaseTemplateRepository> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public bool HasTemplate(IPowerPointTheme theme, SlideOrientation orientation)
    {
        if (theme.Engine != PowerPointThemeEngine.Hybrid || string.IsNullOrWhiteSpace(theme.BaseTemplateKey))
            return false;

        var path = ResolveDiskPath(theme.BaseTemplateKey!, orientation);
        if (File.Exists(path))
            return true;

        return PowerPointThemeLibrary.AllDefinitions.Any(d =>
            d.Template == theme.Template && d.Engine == PowerPointThemeEngine.Hybrid);
    }

    public byte[] GetTemplateBytes(IPowerPointTheme theme, SlideOrientation orientation)
    {
        var cacheKey = $"{theme.Template}:{orientation}";
        return _cache.GetOrAdd(cacheKey, _ => LoadOrBuild(theme, orientation));
    }

    private byte[] LoadOrBuild(IPowerPointTheme theme, SlideOrientation orientation)
    {
        var key = theme.BaseTemplateKey ?? theme.Template.ToString();
        var diskPath = ResolveDiskPath(key, orientation);
        if (File.Exists(diskPath))
        {
            _logger.LogDebug("Loading hybrid template {Key} from {Path}", key, diskPath);
            return File.ReadAllBytes(diskPath);
        }

        var definition = PowerPointThemeLibrary.AllDefinitions.First(d => d.Template == theme.Template);
        _logger.LogDebug("Generating hybrid template {Template} in-memory", theme.Template);
        return PowerPointBaseTemplateBuilder.Build(definition, orientation);
    }

    private string ResolveDiskPath(string key, SlideOrientation orientation)
    {
        var suffix = orientation == SlideOrientation.Widescreen16x9 ? "16x9" : "4x3";
        return Path.Combine(
            _environment.ContentRootPath,
            "wwwroot",
            "assets",
            "powerpoint",
            "themes",
            key,
            $"base-{suffix}.pptx");
    }
}
