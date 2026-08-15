using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Treasury;

/// <summary>
/// Réglages du prévisionnel de trésorerie pour le tenant : seuils de la jauge « Position de
/// trésorerie » et convention de paiement de la paie. Une seule ligne par base tenant.
/// </summary>
/// <remarks>
/// Les seuils ne sont pas des constantes du code : ce qui est confortable pour une société vaut
/// une tension pour une autre. À défaut de saisie, l'appelant les dérive de la masse salariale
/// mensuelle moyenne, qui est le meilleur ordre de grandeur disponible sans rien demander.
/// </remarks>
public sealed class CashFlowForecastSettings : Entity
{
    /// <summary>Sous ce solde, la position est critique (rouge). Souvent négatif ou nul.</summary>
    public decimal CriticalThreshold { get; private set; }

    /// <summary>Sous ce solde, la position appelle une vigilance (orange).</summary>
    public decimal AlertThreshold { get; private set; }

    /// <summary>Au-dessus de ce solde, la position est confortable (vert).</summary>
    public decimal ComfortThreshold { get; private set; }

    /// <summary>
    /// Jour de paiement des salaires propre au tenant. Null ⇒ la valeur de configuration
    /// <c>TreasuryForecast:PayrollPaymentDayOfMonth</c> s'applique.
    /// </summary>
    public int? PayrollPaymentDayOfMonth { get; private set; }

    private CashFlowForecastSettings() { }

    public static CashFlowForecastSettings CreateDefault(decimal averageMonthlyPayrollCost)
    {
        // Un mois de masse salariale comme matelas de confort, la moitié comme seuil d'alerte :
        // repère usuel de gestion, et surtout un défaut qui a du sens dès le premier affichage.
        var comfort = MillimeRounding.Round(Math.Max(0m, averageMonthlyPayrollCost));
        var alert = MillimeRounding.Round(comfort / 2m);

        return new CashFlowForecastSettings
        {
            CriticalThreshold = 0m,
            AlertThreshold = alert,
            ComfortThreshold = comfort
        };
    }

    public void Update(
        decimal criticalThreshold,
        decimal alertThreshold,
        decimal comfortThreshold,
        int? payrollPaymentDayOfMonth)
    {
        if (alertThreshold < criticalThreshold)
            throw new ArgumentException(
                "Le seuil d'alerte doit être ≥ au seuil critique.",
                nameof(alertThreshold));

        if (comfortThreshold < alertThreshold)
            throw new ArgumentException(
                "Le seuil de confort doit être ≥ au seuil d'alerte.",
                nameof(comfortThreshold));

        if (payrollPaymentDayOfMonth is < 1 or > 31)
            throw new ArgumentException(
                "Le jour de paiement de la paie doit être dans [1..31].",
                nameof(payrollPaymentDayOfMonth));

        CriticalThreshold = MillimeRounding.Round(criticalThreshold);
        AlertThreshold = MillimeRounding.Round(alertThreshold);
        ComfortThreshold = MillimeRounding.Round(comfortThreshold);
        PayrollPaymentDayOfMonth = payrollPaymentDayOfMonth;
    }
}
