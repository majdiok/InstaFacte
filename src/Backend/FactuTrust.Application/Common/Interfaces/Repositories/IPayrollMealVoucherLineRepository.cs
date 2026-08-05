using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IPayrollMealVoucherLineRepository
{
    Task<PayrollMealVoucherLine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollMealVoucherLine>> ListForMonthAsync(int year, int month, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollMealVoucherLine>> ListByEmployeeAndMonthAsync(
        Guid employeeId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<PayrollMealVoucherLine> AddAsync(PayrollMealVoucherLine entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(PayrollMealVoucherLine entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(PayrollMealVoucherLine entity, CancellationToken cancellationToken = default);
}
