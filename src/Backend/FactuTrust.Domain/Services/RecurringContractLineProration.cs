namespace FactuTrust.Domain.Services;

/// <summary>Proratisation d'une ligne sur une période, bornée par le contrat et la fenêtre d'effet de la ligne.</summary>
public static class RecurringContractLineProration
{
    public static decimal Prorate(
        DateTime periodFrom, DateTime periodTo,
        DateTime contractStart, DateTime? contractEnd,
        DateTime lineEffectiveFrom, DateTime? lineEffectiveTo,
        decimal fullAmount)
    {
        if (contractStart > periodFrom || (contractEnd.HasValue && contractEnd.Value < periodTo))
        {
            var start = contractStart > periodFrom ? contractStart : periodFrom;
            var end = contractEnd.HasValue && contractEnd.Value < periodTo ? contractEnd.Value : periodTo;
            return ProrationCalculator.ProrateAmount(fullAmount, periodFrom, periodTo, start, end);
        }
        var effectiveFrom = lineEffectiveFrom > periodFrom ? lineEffectiveFrom : periodFrom;
        var effectiveTo = lineEffectiveTo.HasValue && lineEffectiveTo.Value < periodTo ? lineEffectiveTo.Value : periodTo;
        if (effectiveFrom > periodFrom || effectiveTo < periodTo)
            return ProrationCalculator.ProrateAmount(fullAmount, periodFrom, periodTo, effectiveFrom, effectiveTo);
        return fullAmount;
    }
}
