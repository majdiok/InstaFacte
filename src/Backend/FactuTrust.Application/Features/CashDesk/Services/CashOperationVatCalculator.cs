using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.CashDesk.Services;

/// <summary>
/// Décompose un montant TTC de caisse en HT / TVA pour un taux donné. Fonction pure et
/// statique, sans dépendance à un contexte : <c>Amount</c> reste le TTC saisi (aucun changement
/// de saisie), la TVA est calculée en reste pour garantir HT + TVA == TTC exactement (écriture
/// toujours équilibrée), cohérent avec <c>Money.DecimalPlaces = 3</c>.
/// </summary>
public static class CashOperationVatCalculator
{
    /// <summary>
    /// Décompose un montant TTC selon le taux fourni.
    /// HT = Round(TTC / (1 + taux/100), 3, AwayFromZero) ; TVA = TTC - HT (reste, jamais recalculé
    /// indépendamment, garantit l'équilibre de l'écriture même aux bornes d'arrondi).
    /// </summary>
    public static (decimal Ht, decimal Vat) SplitTtc(decimal ttcAmount, VatRate rate)
    {
        var ht = Math.Round(ttcAmount / (1 + rate.ToPercentage()), 3, MidpointRounding.AwayFromZero);
        var vat = ttcAmount - ht;
        return (ht, vat);
    }

    /// <summary>
    /// Variante nullable pour le mapping DTO (<c>VatRatePercent</c>/<c>HtAmount</c>/<c>VatAmount</c>) :
    /// retourne <c>null</c> quand le taux n'est pas renseigné sur l'opération.
    /// </summary>
    public static (int RatePercent, decimal Ht, decimal Vat)? TrySplitTtc(decimal ttcAmount, VatRate? rate)
    {
        if (rate is not { } r)
            return null;

        var (ht, vat) = SplitTtc(ttcAmount, r);
        return ((int)r, ht, vat);
    }
}
