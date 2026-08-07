using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IPayrollPublicHolidayRepository
{
    Task<PayrollPublicHoliday?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsForDateAsync(int year, DateTime date, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollPublicHoliday>> ListByYearAsync(int year, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollPublicHoliday>> ListForMonthAsync(int year, int month, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> ListConfiguredYearsAsync(CancellationToken cancellationToken = default);

    Task<PayrollPublicHoliday> AddAsync(PayrollPublicHoliday entity, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<PayrollPublicHoliday> entities, CancellationToken cancellationToken = default);

    Task UpdateAsync(PayrollPublicHoliday entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(PayrollPublicHoliday entity, CancellationToken cancellationToken = default);
}
