using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IAccountingPeriodRepository
{
    Task<AccountingPeriod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AccountingPeriod?> GetByYearMonthAsync(int fiscalYear, int month, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountingPeriod>> GetByFiscalYearAsync(int fiscalYear, CancellationToken cancellationToken = default);
    Task<AccountingPeriod> AddAsync(AccountingPeriod entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(AccountingPeriod entity, CancellationToken cancellationToken = default);
}
