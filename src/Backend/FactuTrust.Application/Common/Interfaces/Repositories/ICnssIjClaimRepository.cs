using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>Repository des créances IJ CNSS (subrogation / maternité).</summary>
public interface ICnssIjClaimRepository : IRepository<CnssIjClaim>
{
    Task<IReadOnlyList<CnssIjClaim>> ListByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CnssIjClaim>> ListForPeriodAsync(int year, int? month, CancellationToken cancellationToken = default);

    Task<bool> ExistsForLeaveAndPeriodAsync(Guid leaveRequestId, int year, int month, CancellationToken cancellationToken = default);
}
