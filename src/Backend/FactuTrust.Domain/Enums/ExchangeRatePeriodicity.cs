namespace FactuTrust.Domain.Enums;

/// <summary>
/// Granularité de la table des taux de change d'une devise, pour un exercice donné.
/// </summary>
public enum ExchangeRatePeriodicity
{
    /// <summary>Un taux unique pour tout l'exercice (stocké avec <c>Month = null</c>).</summary>
    Fixe = 0,

    /// <summary>Un taux par mois de l'exercice (stocké avec <c>Month = 1..12</c>).</summary>
    Mensuelle = 1
}
