namespace FactuTrust.Domain.Entities.Storefront;

/// <summary>
/// Lifecycle of a public (guest) order received through the 3D street.
/// Keeps the order auditable across all tenants it was dispatched to.
/// </summary>
public enum StorefrontOrderStatus
{
    Submitted = 0,
    DispatchedToTenants = 1,
    PartiallyConfirmed = 2,
    Confirmed = 3,
    Cancelled = 4,
    Failed = 5
}

public static class StorefrontOrderStatusExtensions
{
    public static string ToDisplayString(this StorefrontOrderStatus status) => status switch
    {
        StorefrontOrderStatus.Submitted => "Reçue",
        StorefrontOrderStatus.DispatchedToTenants => "Transmise aux sociétés",
        StorefrontOrderStatus.PartiallyConfirmed => "Partiellement confirmée",
        StorefrontOrderStatus.Confirmed => "Confirmée",
        StorefrontOrderStatus.Cancelled => "Annulée",
        StorefrontOrderStatus.Failed => "Échec",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
