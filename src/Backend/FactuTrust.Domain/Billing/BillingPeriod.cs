namespace FactuTrust.Domain.Billing;

/// <summary>Périodicité de facturation d'un Plan plateforme (Lot C1).</summary>
public enum BillingPeriod
{
    /// <summary>Plan gratuit, sans périodicité.</summary>
    Free = 0,
    Monthly = 1,
    Annual = 2,
    /// <summary>Paiement unique à vie (lifetime / one-shot).</summary>
    OneShot = 3
}

public static class BillingPeriodExtensions
{
    public static string ToDisplayString(this BillingPeriod period) => period switch
    {
        BillingPeriod.Free => "Gratuit",
        BillingPeriod.Monthly => "Mensuel",
        BillingPeriod.Annual => "Annuel",
        BillingPeriod.OneShot => "Paiement unique",
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };
}
