namespace FactuTrust.Domain.Entities.Storefront;

/// <summary>
/// Stock visibility label shown publicly. We intentionally never expose the
/// exact quantity to avoid leaking commercial data and to keep the public
/// projection stable between syncs.
/// </summary>
public enum StockDisplayStatus
{
    InStock = 0,
    Limited = 1,
    OnDemand = 2,
    OutOfStock = 3
}

public static class StockDisplayStatusExtensions
{
    public static string ToDisplayString(this StockDisplayStatus status) => status switch
    {
        StockDisplayStatus.InStock => "En stock",
        StockDisplayStatus.Limited => "Stock limité",
        StockDisplayStatus.OnDemand => "Sur commande",
        StockDisplayStatus.OutOfStock => "Rupture temporaire",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
