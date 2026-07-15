using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository for <see cref="BankDeposit"/> aggregates.
/// </summary>
public interface IBankDepositRepository : IRepository<BankDeposit>
{
    Task<(IReadOnlyList<BankDeposit> Items, int TotalCount)> GetNonCancelledByDateRangeAsync(
        DateTime fromDate,
        DateTime toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new bank deposit and its linked cash debit in a single transaction.
    /// </summary>
    Task AddWithCashOperationAsync(BankDeposit deposit, CashOperation cashOperation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists updates to a bank deposit and its linked cash operation in a single transaction.
    /// </summary>
    Task UpdateBankDepositAndCashOperationAsync(
        BankDeposit deposit,
        CashOperation cashOperation,
        CancellationToken cancellationToken = default);
}
