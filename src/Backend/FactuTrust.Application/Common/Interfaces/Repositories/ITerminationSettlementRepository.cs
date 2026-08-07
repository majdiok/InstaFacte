using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ITerminationSettlementRepository
{
    Task<TerminationSettlement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TerminationSettlement?> GetByEmployeeAndMonthAsync(
        Guid employeeId, int year, int month, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TerminationSettlement>> ListForMonthAsync(
        int year, int month, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TerminationSettlement>> ListAsync(CancellationToken cancellationToken = default);

    Task<TerminationSettlement> AddAsync(TerminationSettlement entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(TerminationSettlement entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(TerminationSettlement entity, CancellationToken cancellationToken = default);
}
