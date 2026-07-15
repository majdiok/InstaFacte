namespace FactuTrust.Domain.Enums;

/// <summary>
/// Cycle de vie d'un effet de commerce (traite) porté par un paiement dont
/// <see cref="PaymentMethod"/> vaut <see cref="PaymentMethod.Traite"/>.
/// Null sur les paiements non-traite.
/// </summary>
public enum EffetStatus
{
    /// <summary>Effet reçu/accepté, en attente de son échéance (créance/dette portée par 412/403).</summary>
    EnPortefeuille = 0,

    /// <summary>Effet encaissé (client) ou payé (fournisseur) à échéance : la trésorerie a bougé.</summary>
    Encaisse = 1,

    /// <summary>Effet client revenu impayé : la créance est réouverte sur le compte client.</summary>
    Impaye = 2
}

public static class EffetStatusExtensions
{
    public static string ToDisplayString(this EffetStatus status) => status switch
    {
        EffetStatus.EnPortefeuille => "En portefeuille",
        EffetStatus.Encaisse => "Encaissé",
        EffetStatus.Impaye => "Impayé",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
