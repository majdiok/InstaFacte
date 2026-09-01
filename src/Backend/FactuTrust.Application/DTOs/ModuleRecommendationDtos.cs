namespace FactuTrust.Application.DTOs;

/// <summary>
/// Response item for <c>GET /api/company/module-recommendations</c> (plan §3.3). Explainable,
/// rule-based suggestions — never auto-applied, always dismissible by the tenant.
/// </summary>
public sealed record ModuleRecommendationDto
{
    public required int ModuleId { get; init; }

    /// <summary>Stable machine-readable discriminator, e.g. "high-quote-volume".</summary>
    public required string ReasonCode { get; init; }

    /// <summary>Human-readable French explanation shown in the UI.</summary>
    public required string ReasonFr { get; init; }
}
