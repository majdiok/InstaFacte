using DocumentFormat.OpenXml.Packaging;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using P = DocumentFormat.OpenXml.Presentation;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Templates;

/// <summary>
/// Resolves slide kinds to layout parts by fixed build order from <see cref="PowerPointBaseTemplateBuilder"/>.
/// </summary>
public sealed class TemplateLayoutMapper
{
    private readonly IReadOnlyDictionary<PowerPointSlideKind, SlideLayoutPart> _byKind;
    private readonly SlideLayoutPart _fallback;

    public TemplateLayoutMapper(SlideMasterPart masterPart)
    {
        var layouts = masterPart.SlideLayoutParts.ToList();
        var kinds = HybridLayoutNames.StandardMappings.Select(m => m.SlideKind).ToList();
        var map = new Dictionary<PowerPointSlideKind, SlideLayoutPart>();

        for (var i = 0; i < kinds.Count && i < layouts.Count; i++)
            map[kinds[i]] = layouts[i];

        _fallback = map.GetValueOrDefault(PowerPointSlideKind.Blank)
                      ?? layouts.FirstOrDefault()
                      ?? throw new InvalidOperationException("Hybrid template has no slide layouts.");
        _byKind = map;
    }

    public SlideLayoutPart Resolve(PowerPointSlideKind kind) =>
        _byKind.TryGetValue(kind, out var part) ? part : _fallback;

    public IReadOnlyDictionary<PowerPointSlideKind, SlideLayoutPart> All => _byKind;
}
