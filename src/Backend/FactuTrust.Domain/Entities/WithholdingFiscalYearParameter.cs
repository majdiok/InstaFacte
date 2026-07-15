using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Paramètres de retenue à la source versionnés par exercice (ex. seuil TTC RS7).
/// Les valeurs par défaut sont initialisées pour chaque année ; la LF peut imposer des mises à jour sans modifier le code.
/// </summary>
public sealed class WithholdingFiscalYearParameter : Entity
{
    public int FiscalYear { get; private set; }
    /// <summary>Seuil TTC (TND) en dessous duquel la RS7 ne s'applique pas.</summary>
    public decimal Rs7TtcThresholdTnd { get; private set; }

    private WithholdingFiscalYearParameter() { }

    public static WithholdingFiscalYearParameter Create(int fiscalYear, decimal rs7TtcThresholdTnd)
    {
        if (fiscalYear < 2000 || fiscalYear > 2100)
            throw new ArgumentOutOfRangeException(nameof(fiscalYear));
        if (rs7TtcThresholdTnd < 0)
            throw new ArgumentOutOfRangeException(nameof(rs7TtcThresholdTnd));

        return new WithholdingFiscalYearParameter
        {
            FiscalYear = fiscalYear,
            Rs7TtcThresholdTnd = Math.Round(rs7TtcThresholdTnd, 3)
        };
    }

    public void SetRs7Threshold(decimal rs7TtcThresholdTnd)
    {
        if (rs7TtcThresholdTnd < 0)
            throw new ArgumentOutOfRangeException(nameof(rs7TtcThresholdTnd));
        Rs7TtcThresholdTnd = Math.Round(rs7TtcThresholdTnd, 3);
    }
}
