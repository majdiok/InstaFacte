namespace FactuTrust.Application.Features.AI.Export.PowerPoint.Models;

/// <summary>
/// Centralised limits and constants for the PowerPoint export pipeline. Keeping them in one
/// place lets validators, generators and tests stay in sync.
/// </summary>
public static class PowerPointDeckLimits
{
    public const int MinResponses = 1;
    public const int MaxResponses = 50;

    public const int TitleMaxLength = 200;
    public const int SubtitleMaxLength = 250;
    public const int AuthorNameMaxLength = 200;
    public const int CustomTitleMaxLength = 200;
    public const int LocaleMaxLength = 16;

    public const string DefaultLocale = "fr-TN";

    /// <summary>Hard cap on slides generated for a single export. Protects the renderer from runaway prompts.</summary>
    public const int MaxSlidesPerDeck = 200;

    /// <summary>Maximum characters of markdown content rendered per slide before pagination kicks in.</summary>
    public const int MaxCharsPerTextSlide = 900;

    /// <summary>Maximum bullets rendered per slide before pagination kicks in.</summary>
    public const int MaxBulletsPerTextSlide = 7;

    /// <summary>Maximum rows rendered in a single table slide. Larger tables are paginated.</summary>
    public const int MaxRowsPerTableSlide = 14;

    /// <summary>Maximum columns rendered in a single table slide. Larger tables are truncated with a note.</summary>
    public const int MaxColumnsPerTableSlide = 8;

    /// <summary>Default TTL for signed download links.</summary>
    public static readonly TimeSpan DownloadLinkTtl = TimeSpan.FromHours(1);

    /// <summary>Default retention for export files on disk before the cleanup job removes them.</summary>
    public static readonly TimeSpan StorageRetention = TimeSpan.FromHours(24);
}
