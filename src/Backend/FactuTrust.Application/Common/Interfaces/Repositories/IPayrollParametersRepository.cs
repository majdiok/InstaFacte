using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for payroll year parameters (barème IRPP, taux CNSS/CSS/TFP/FOPROLOS).
/// </summary>
public interface IPayrollParametersRepository
{
    Task<PayrollYearParameters?> GetByFiscalYearAsync(int fiscalYear, CancellationToken cancellationToken = default);

    /// <summary>Returns the parameters for a year, seeding the legal defaults on first access.</summary>
    Task<PayrollYearParameters> GetOrCreateForYearAsync(int fiscalYear, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollYearParameters>> ListAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(PayrollYearParameters parameters, CancellationToken cancellationToken = default);
}
