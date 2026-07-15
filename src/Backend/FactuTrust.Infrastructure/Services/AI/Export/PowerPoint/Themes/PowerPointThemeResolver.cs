using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;

/// <summary>
/// Resolves themes by enum value. Falls back to <see cref="PowerPointTemplate.Standard"/> when the requested
/// template is unknown — keeps the export pipeline forward-compatible with newer client builds.
/// </summary>
public sealed class PowerPointThemeResolver : IPowerPointThemeResolver
{
    private readonly IReadOnlyDictionary<PowerPointTemplate, IPowerPointTheme> _byTemplate;
    private readonly IReadOnlyList<IPowerPointTheme> _all;

    public PowerPointThemeResolver(
        IBrandAssetProvider brandAssets,
        IOptions<PowerPointRenderingOptions>? renderingOptions = null)
    {
        var logo = brandAssets.GetLogoPng();
        var extended = renderingOptions?.Value?.ExtendedThemeLibraryEnabled ?? true;

        var definitions = extended
            ? PowerPointThemeLibrary.AllDefinitions
            : PowerPointThemeLibrary.AllDefinitions.Where(d => (int)d.Template <= (int)PowerPointTemplate.Executive);

        _all = definitions
            .OrderBy(d => d.SortOrder)
            .Select(d => (IPowerPointTheme)new ConfigurablePowerPointTheme(d, logo))
            .ToList()
            .AsReadOnly();

        _byTemplate = _all.ToDictionary(t => t.Template);
    }

    public IPowerPointTheme Resolve(PowerPointTemplate template) =>
        _byTemplate.TryGetValue(template, out var theme) ? theme : _byTemplate[PowerPointTemplate.Standard];

    public IReadOnlyList<IPowerPointTheme> All => _all;
}