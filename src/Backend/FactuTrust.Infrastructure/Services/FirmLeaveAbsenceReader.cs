using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

/// <inheritdoc cref="IFirmLeaveAbsenceReader"/>
public sealed class FirmLeaveAbsenceReader : IFirmLeaveAbsenceReader
{
    private readonly MasterDbContext _master;

    public FirmLeaveAbsenceReader(MasterDbContext master) => _master = master;

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetApprovedAbsenceDaysByUserAsync(
        Guid firmTenantId,
        int year,
        CancellationToken cancellationToken = default)
    {
        // Seuls les congés acceptés comptent, et seulement pour les types que le cabinet a
        // désignés comme décomptés du temps de présence : le télétravail est du temps travaillé,
        // et la formation est déjà absorbée par le taux de productivité de l'exercice.
        var rows = await (
            from r in _master.FirmLeaveRequests.AsNoTracking()
            join t in _master.FirmLeaveTypes.AsNoTracking() on r.LeaveTypeId equals t.Id
            where r.FirmTenantId == firmTenantId
                  && r.Status == FirmLeaveRequestStatus.Approved
                  && r.StartDate.Year == year
                  && t.CountsAsAbsence
            select new { r.UserId, r.Days })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.UserId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Days));
    }
}
