using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Services;

public sealed record StockVoucherLineAllocationsDto(
    Guid LineId,
    IReadOnlyList<StockAllocationInput> Allocations);

public interface IStockVoucherMovementService
{
    Task<Result> ValidateAsync(Guid stockVoucherId, CancellationToken cancellationToken = default);

    Task<Result> ValidateAsync(
        Guid stockVoucherId,
        IReadOnlyList<StockVoucherLineAllocationsDto>? lineAllocations,
        CancellationToken cancellationToken = default);

    Task<Result> CancelAsync(Guid stockVoucherId, string reason, CancellationToken cancellationToken = default);
}
