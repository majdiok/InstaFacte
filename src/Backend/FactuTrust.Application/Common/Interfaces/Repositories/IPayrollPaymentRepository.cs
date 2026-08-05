using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>Repository pour les paiements de paie (lien trésorerie).</summary>
public interface IPayrollPaymentRepository
{
    Task<PayrollPayment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PayrollPayment?> GetByIdWithLinesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollPayment>> ListByPayrollRunAsync(Guid runId, bool includeCancelled, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollPaymentLine>> ListLinesByPayslipAsync(Guid payslipId, CancellationToken cancellationToken = default);
    Task AddAsync(PayrollPayment payment, CancellationToken cancellationToken = default);
    Task UpdateAsync(PayrollPayment payment, CancellationToken cancellationToken = default);
    Task UpdatePayslipsAsync(IEnumerable<Payslip> payslips, CancellationToken cancellationToken = default);
}
