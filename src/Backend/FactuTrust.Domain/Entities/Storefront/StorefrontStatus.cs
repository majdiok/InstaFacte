namespace FactuTrust.Domain.Entities.Storefront;

/// <summary>
/// Lifecycle state of a public storefront. Platform moderators control transitions to
/// <see cref="Published"/> and <see cref="Suspended"/>; the tenant owner controls
/// <see cref="Draft"/> and <see cref="PendingReview"/> transitions.
/// </summary>
public enum StorefrontStatus
{
    Draft = 0,
    PendingReview = 1,
    Published = 2,
    Suspended = 3
}

public static class StorefrontStatusExtensions
{
    public static string ToDisplayString(this StorefrontStatus status) => status switch
    {
        StorefrontStatus.Draft => "Brouillon",
        StorefrontStatus.PendingReview => "En attente de validation",
        StorefrontStatus.Published => "Publiée",
        StorefrontStatus.Suspended => "Suspendue",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
