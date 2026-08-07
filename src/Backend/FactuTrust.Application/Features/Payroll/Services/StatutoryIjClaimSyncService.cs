using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Services.Payroll;

namespace FactuTrust.Application.Features.Payroll.Services;

/// <summary>Synchronise les créances IJ CNSS à partir des montants statutaires calculés.</summary>
public sealed class StatutoryIjClaimSyncService
{
    private readonly ICnssIjClaimRepository _claims;

    public StatutoryIjClaimSyncService(ICnssIjClaimRepository claims)
    {
        _claims = claims;
    }

    public async Task SyncAsync(
        Guid employeeId,
        int year,
        int month,
        StatutoryLeavePayrollAggregator.StatutoryLeaveAmounts amounts,
        CancellationToken cancellationToken)
    {
        foreach (var draft in amounts.IjClaims)
        {
            if (await _claims.ExistsForLeaveAndPeriodAsync(draft.LeaveRequestId, year, month, cancellationToken))
                continue;

            var create = CnssIjClaim.Create(employeeId, draft.LeaveRequestId, year, month, draft.Amount);
            if (create.IsFailure)
                continue;

            await _claims.AddAsync(create.Value, cancellationToken);
        }
    }
}
