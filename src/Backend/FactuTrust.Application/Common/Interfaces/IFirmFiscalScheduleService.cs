using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces;

public interface IFirmFiscalScheduleService
{
    Task<FiscalScheduleListDto> GetScheduleAsync(
        Guid firmTenantId,
        FiscalScheduleFiltersDto filters,
        CancellationToken cancellationToken = default);
}
