using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>Repository pour les versements CNSS mensuels.</summary>
public interface ICnssContributionPaymentRepository
{
    Task<CnssContributionPayment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CnssContributionPayment?> GetActiveByPeriodAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<bool> HasActivePaymentForPeriodAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<bool> HasActivePaymentForRunAsync(
        Guid payrollRunId,
        CancellationToken cancellationToken = default);

    Task AddAsync(CnssContributionPayment payment, CancellationToken cancellationToken = default);

    Task UpdateAsync(CnssContributionPayment payment, CancellationToken cancellationToken = default);
}
