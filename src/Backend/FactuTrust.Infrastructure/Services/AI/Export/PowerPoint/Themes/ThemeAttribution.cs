namespace FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;

/// <summary>License attribution required for third-party theme assets.</summary>
public sealed record ThemeAttribution(
    string Source,
    string License,
    string AttributionText,
    IReadOnlyList<string> AssetCredits);
