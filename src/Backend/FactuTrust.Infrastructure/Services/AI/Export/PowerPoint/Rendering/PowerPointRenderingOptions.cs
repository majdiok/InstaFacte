namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering;

/// <summary>
/// Feature flags for the PowerPoint export rendering pipeline. Bound from
/// <c>AiExport:PowerPoint</c> in appsettings.
/// </summary>
public sealed class PowerPointRenderingOptions
{
    public const string SectionName = "AiExport:PowerPoint";

    public bool EnhancedRenderingEnabled { get; set; } = true;

    public bool SemanticSectionSlidesEnabled { get; set; } = true;

    public bool MarkdownTableExtractionEnabled { get; set; } = true;

    /// <summary>
    /// When false, only templates 0–2 (Standard, Analyse, Executive) are exposed and resolved.
    /// </summary>
    public bool ExtendedThemeLibraryEnabled { get; set; } = true;

    /// <summary>When false, all exports use the legacy programmatic engine.</summary>
    public bool TemplateHybridEnabled { get; set; }

    /// <summary>Templates with Id below this value stay on legacy when hybrid is enabled.</summary>
    public int HybridThemeMinId { get; set; } = 3;

    /// <summary>Append attribution slide for themes that require third-party credits.</summary>
    public bool RequireAttributionSlide { get; set; } = true;
}
