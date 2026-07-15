using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Builders;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Templates;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using P = DocumentFormat.OpenXml.Presentation;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;

/// <summary>
/// Mutable build-time context shared by every slide builder for a single deck. Holds the
/// presentation document being assembled, the theme, the slide dimensions and per-deck counters.
/// </summary>
public sealed class SlideBuildContext
{
    private TemplateLayoutMapper? _layoutMapper;

    public SlideBuildContext(
        PresentationDocument document,
        SlideMasterPart masterPart,
        SlideLayoutPart layoutPart,
        IPowerPointTheme theme,
        SlideDimensions dimensions,
        PowerPointExportRequestDto request,
        CultureInfo culture,
        string authorName,
        PowerPointRenderingOptions renderingOptions)
    {
        Document = document;
        MasterPart = masterPart;
        LayoutPart = layoutPart;
        ActiveLayoutPart = layoutPart;
        Theme = theme;
        Dimensions = dimensions;
        Request = request;
        Culture = culture;
        AuthorName = authorName;
        RenderingOptions = renderingOptions;
    }

    public PresentationDocument Document { get; }
    public SlideMasterPart MasterPart { get; }
    public SlideLayoutPart LayoutPart { get; }
    public SlideLayoutPart ActiveLayoutPart { get; set; }
    public IPowerPointTheme Theme { get; }
    public SlideDimensions Dimensions { get; }
    public PowerPointExportRequestDto Request { get; }
    public CultureInfo Culture { get; }
    public string AuthorName { get; }
    public PowerPointRenderingOptions RenderingOptions { get; }

    /// <summary>When true, cover/content builders skip full-bleed fills (master provides background).</summary>
    public bool UseHybridMasterBackground { get; set; }

    public void ConfigureHybridLayouts(TemplateLayoutMapper mapper) => _layoutMapper = mapper;

    public SlideLayoutPart ResolveLayout(PowerPointSlideKind kind) =>
        _layoutMapper?.Resolve(kind) ?? ActiveLayoutPart;

    /// <summary>Total slide parts created so far. Used as a stable ID generator (avoids ID collisions).</summary>
    public int SlideCount { get; private set; }

    /// <summary>Running page counter for content slides (used by footers).</summary>
    public int ContentPageNumber { get; private set; }

    public uint NextShapeId() => (uint)Interlocked.Increment(ref _shapeIdCounter);
    private int _shapeIdCounter = 1000;

    public void IncrementSlideCount()
    {
        SlideCount++;
        ContentPageNumber++;
    }

    public void AppendContentFooter(P.ShapeTree tree)
    {
        SlideHeaderFooterDecorator.AppendFooter(this, tree, ContentPageNumber, TotalContentPagesHint);
    }

    /// <summary>Best-effort total for footer display; updated by orchestrator when known.</summary>
    public int TotalContentPagesHint { get; set; } = 1;
}
