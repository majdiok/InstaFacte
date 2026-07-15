using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;

namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Templates;

/// <summary>Maps a logical slide kind to a layout name inside a hybrid base template.</summary>
public sealed record LayoutMapping(PowerPointSlideKind SlideKind, string LayoutName);

/// <summary>Standard layout names enforced by the template sanitizer / builder.</summary>
public static class HybridLayoutNames
{
    public const string Cover = "Layout_Cover";
    public const string Section = "Layout_Section";
    public const string TitleAndContent = "Layout_TitleAndContent";
    public const string TwoColumn = "Layout_TwoColumn";
    public const string Blank = "Layout_Blank";
    public const string Credits = "Layout_Credits";

    public static IReadOnlyList<LayoutMapping> StandardMappings { get; } = new[]
    {
        new LayoutMapping(PowerPointSlideKind.Cover, Cover),
        new LayoutMapping(PowerPointSlideKind.Agenda, TitleAndContent),
        new LayoutMapping(PowerPointSlideKind.TableOfContents, TitleAndContent),
        new LayoutMapping(PowerPointSlideKind.SectionDivider, Section),
        new LayoutMapping(PowerPointSlideKind.TitleAndContent, TitleAndContent),
        new LayoutMapping(PowerPointSlideKind.TwoColumn, TwoColumn),
        new LayoutMapping(PowerPointSlideKind.Blank, Blank),
        new LayoutMapping(PowerPointSlideKind.Credits, Credits)
    };
}
