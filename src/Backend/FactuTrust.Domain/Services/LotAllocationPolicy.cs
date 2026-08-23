using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services;

public sealed record LotCandidate(
    Guid ProductLotId,
    string LotNumber,
    DateTime? ExpiryDate,
    DateTime? FirstReceivedAt,
    decimal QuantityAvailable);

public sealed record LotAllocation(
    Guid ProductLotId,
    string LotNumber,
    decimal Quantity);

/// <summary>Pure FEFO / FIFO-physical allocation over lot balances.</summary>
public static class LotAllocationPolicy
{
    public static IReadOnlyList<LotCandidate> OrderForPicking(
        IEnumerable<LotCandidate> candidates,
        PickingPolicy policy)
    {
        var list = candidates.Where(c => c.QuantityAvailable > 0).ToList();
        return policy switch
        {
            PickingPolicy.Fefo => list
                .OrderBy(c => c.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(c => c.FirstReceivedAt ?? DateTime.MaxValue)
                .ThenBy(c => c.LotNumber, StringComparer.Ordinal)
                .ToList(),
            PickingPolicy.FifoPhysical => list
                .OrderBy(c => c.FirstReceivedAt ?? DateTime.MaxValue)
                .ThenBy(c => c.LotNumber, StringComparer.Ordinal)
                .ToList(),
            _ => list
                .OrderBy(c => c.LotNumber, StringComparer.Ordinal)
                .ToList()
        };
    }

    public static Result<IReadOnlyList<LotAllocation>> Allocate(
        decimal quantity,
        IReadOnlyList<LotCandidate> orderedCandidates,
        bool blockExpired,
        DateTime utcNow)
    {
        if (quantity <= 0)
            return Result.Failure<IReadOnlyList<LotAllocation>>(
                Error.Validation("Quantity", "La quantité doit être positive"));

        var remaining = quantity;
        var allocations = new List<LotAllocation>();

        foreach (var candidate in orderedCandidates)
        {
            if (remaining <= 0)
                break;

            if (blockExpired && candidate.ExpiryDate.HasValue && candidate.ExpiryDate.Value.Date < utcNow.Date)
                continue;

            var take = Math.Min(remaining, candidate.QuantityAvailable);
            if (take <= 0)
                continue;

            allocations.Add(new LotAllocation(candidate.ProductLotId, candidate.LotNumber, take));
            remaining -= take;
        }

        if (remaining > 0)
        {
            return Result.Failure<IReadOnlyList<LotAllocation>>(Error.Validation(
                "Quantity",
                $"Stock de lots insuffisant. Demandé: {quantity}, alloué: {quantity - remaining}"));
        }

        return Result.Success<IReadOnlyList<LotAllocation>>(allocations);
    }
}
