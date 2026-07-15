using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Completes a stock transfer in a single database transaction (stock movements + aggregate state).
/// </summary>
public interface IStockTransferCompletionService
{
    Task<Result> CompleteTransferAsync(Guid stockTransferId, CancellationToken cancellationToken = default);
}
