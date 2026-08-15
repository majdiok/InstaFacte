using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.Treasury;

/// <summary>
/// Agrégat mensuel d'une projection : ce qui alimente le graphique d'évolution et le tableau
/// « Détail mensuel » de l'écran.
/// </summary>
/// <remarks>
/// Le chaînage est un invariant du module : <c>OpeningBalance</c> d'un mois est le
/// <c>ClosingBalance</c> du précédent, et le premier reprend le solde d'ouverture du run.
/// <see cref="Create"/> calcule systématiquement <c>NetFlow</c> et <c>ClosingBalance</c> plutôt
/// que de les accepter en paramètre, pour qu'aucun appelant ne puisse persister une incohérence.
/// </remarks>
public sealed class CashFlowForecastBucket : Entity
{
    public Guid ForecastRunId { get; private set; }

    /// <summary>Premier jour du mois couvert.</summary>
    public DateTime PeriodStart { get; private set; }

    /// <summary>Dernier jour du mois couvert.</summary>
    public DateTime PeriodEnd { get; private set; }

    /// <summary>Rang du mois dans l'horizon, à partir de 0 — garantit un tri stable.</summary>
    public int SequenceIndex { get; private set; }

    public decimal OpeningBalance { get; private set; }

    public decimal Inflows { get; private set; }

    public decimal Outflows { get; private set; }

    public decimal NetFlow { get; private set; }

    public decimal ClosingBalance { get; private set; }

    /// <summary>Borne basse du solde de fin de mois (prévision pessimiste de l'intervalle).</summary>
    public decimal LowClosingBalance { get; private set; }

    /// <summary>Borne haute du solde de fin de mois.</summary>
    public decimal HighClosingBalance { get; private set; }

    private CashFlowForecastBucket() { }

    public static CashFlowForecastBucket Create(
        int sequenceIndex,
        DateTime periodStart,
        DateTime periodEnd,
        decimal openingBalance,
        decimal inflows,
        decimal outflows,
        decimal intervalHalfWidth)
    {
        if (sequenceIndex < 0)
            throw new ArgumentException("SequenceIndex doit être ≥ 0.", nameof(sequenceIndex));

        if (periodEnd < periodStart)
            throw new ArgumentException("PeriodEnd doit être ≥ PeriodStart.", nameof(periodEnd));

        if (inflows < 0m)
            throw new ArgumentException("Les encaissements ne peuvent pas être négatifs.", nameof(inflows));

        if (outflows < 0m)
            throw new ArgumentException("Les décaissements ne peuvent pas être négatifs.", nameof(outflows));

        if (intervalHalfWidth < 0m)
            throw new ArgumentException(
                "La demi-largeur de l'intervalle ne peut pas être négative.",
                nameof(intervalHalfWidth));

        var roundedIn = MillimeRounding.Round(inflows);
        var roundedOut = MillimeRounding.Round(outflows);
        var net = MillimeRounding.Round(roundedIn - roundedOut);
        var opening = MillimeRounding.Round(openingBalance);
        var closing = MillimeRounding.Round(opening + net);
        var halfWidth = MillimeRounding.Round(intervalHalfWidth);

        return new CashFlowForecastBucket
        {
            SequenceIndex = sequenceIndex,
            PeriodStart = periodStart.Date,
            PeriodEnd = periodEnd.Date,
            OpeningBalance = opening,
            Inflows = roundedIn,
            Outflows = roundedOut,
            NetFlow = net,
            ClosingBalance = closing,
            LowClosingBalance = MillimeRounding.Round(closing - halfWidth),
            HighClosingBalance = MillimeRounding.Round(closing + halfWidth)
        };
    }
}
