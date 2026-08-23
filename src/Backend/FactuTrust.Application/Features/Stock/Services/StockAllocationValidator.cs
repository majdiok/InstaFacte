using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;

namespace FactuTrust.Application.Features.Stock.Services;

public interface IStockAllocationValidator
{
    Task<Result> ValidateExitAllocationsAsync(
        Guid productId,
        decimal quantity,
        IReadOnlyList<StockAllocationInput>? allocations,
        CancellationToken cancellationToken = default);
}

public sealed class StockAllocationValidator : IStockAllocationValidator
{
    private readonly IProductRepository _products;

    public StockAllocationValidator(IProductRepository products)
    {
        _products = products;
    }

    public async Task<Result> ValidateExitAllocationsAsync(
        Guid productId,
        decimal quantity,
        IReadOnlyList<StockAllocationInput>? allocations,
        CancellationToken cancellationToken = default)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken);
        if (product is null || !product.IsStockManaged)
            return Result.Success();

        if (product.TrackingMode == TrackingMode.Serial)
        {
            if (allocations is null || allocations.Count == 0)
                return Result.Success();

            if (allocations.Count != (int)quantity || allocations.Any(a => a.Quantity != 1m))
                return Result.Failure(Error.Validation("Allocations",
                    "Chaque numéro de série correspond à une quantité de 1."));

            return Result.Success();
        }

        if (product.TrackingMode == TrackingMode.Lot)
        {
            var needsManual = product.PickingPolicy is PickingPolicy.None or PickingPolicy.Manual;
            if (needsManual && (allocations is null || allocations.Count == 0))
                return Result.Failure(Error.Validation("Allocations",
                    "La sortie d'un article suivi par lot exige une allocation (saisie ou FEFO)."));

            if (allocations is { Count: > 0 })
            {
                var sum = allocations.Sum(a => a.Quantity);
                if (Math.Abs(sum - quantity) > LotBalanceInvariant.Tolerance)
                    return Result.Failure(Error.Validation("Allocations",
                        "La somme des allocations doit être égale à la quantité de la ligne."));
            }
        }

        return Result.Success();
    }
}
