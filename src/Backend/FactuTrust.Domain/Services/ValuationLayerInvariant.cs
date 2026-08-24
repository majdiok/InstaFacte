using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Services;

public static class ValuationLayerInvariant
{
    public const decimal Tolerance = 0.0001m;

    public static Result AssertMatchesOnHand(decimal quantityOnHand, IEnumerable<decimal> remainingQuantities)
    {
        var sum = remainingQuantities.Sum();
        if (Math.Abs(quantityOnHand - sum) > Tolerance)
        {
            return Result.Failure(Error.Validation(
                "ValuationLayer",
                $"Incohérence couches/stock: on-hand {quantityOnHand} ≠ somme couches {sum}"));
        }

        return Result.Success();
    }
}
