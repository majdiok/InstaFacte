using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Services;

public static class LotBalanceInvariant
{
    public const decimal Tolerance = 0.0001m;

    public static Result AssertMatchesOnHand(decimal quantityOnHand, IEnumerable<decimal> lotQuantities)
    {
        var sum = lotQuantities.Sum();
        if (Math.Abs(quantityOnHand - sum) > Tolerance)
        {
            return Result.Failure(Error.Validation(
                "LotBalance",
                $"Incohérence lots/stock: on-hand {quantityOnHand} ≠ somme lots {sum}"));
        }

        return Result.Success();
    }
}
