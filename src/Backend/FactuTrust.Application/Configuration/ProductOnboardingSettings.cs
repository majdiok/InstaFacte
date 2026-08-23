namespace FactuTrust.Application.Configuration;

/// <summary>
/// Kill switch for the first-login product tour and « Premiers pas » checklist.
/// </summary>
public sealed class ProductOnboardingSettings
{
    public const string SectionName = "ProductOnboarding";

    /// <summary>When false, the SPA must not auto-start the tour or show the checklist.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Catalogue version advertised to the SPA (v1 does not re-prompt completed users).</summary>
    public int CatalogVersion { get; set; } = 1;
}
