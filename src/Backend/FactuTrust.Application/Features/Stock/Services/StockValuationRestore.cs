using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Features.Stock.Services;

/// <summary>
/// Builds mutation allocations that restore FIFO/LIFO layers from prior exit movements.
/// Same warehouse: <see cref="StockAllocationInput.RestoreValuationLayerId"/>.
/// Transfer destination: copy <see cref="StockAllocationInput.ReceivedAt"/> / unit cost, never restore ids.
/// </summary>
public static class StockValuationRestore
{
    public static IReadOnlyList<StockAllocationInput> FromExitMovements(
        IEnumerable<StockMovement> exits,
        decimal quantityToRestore,
        bool restoreSameWarehouse,
        Func<Guid, DateTime?>? receivedAtByLayerId = null)
    {
        var remaining = quantityToRestore;
        var list = new List<StockAllocationInput>();
        foreach (var movement in exits
                     .Where(m => m.Type == MovementType.Exit)
                     .OrderBy(m => m.OccurredAt)
                     .ThenBy(m => m.Id))
        {
            if (remaining <= 0)
                break;

            var take = Math.Min(remaining, Math.Abs(movement.Quantity));
            DateTime? receivedAt = null;
            if (!restoreSameWarehouse
                && movement.ValuationLayerId is Guid layerId
                && receivedAtByLayerId is not null)
            {
                receivedAt = receivedAtByLayerId(layerId);
            }

            list.Add(new StockAllocationInput(
                take,
                movement.ProductLotId,
                SerialId: movement.SerialId,
                UnitCost: movement.UnitCost,
                ReceivedAt: receivedAt,
                RestoreValuationLayerId: restoreSameWarehouse ? movement.ValuationLayerId : null));
            remaining -= take;
        }

        return list;
    }
}
