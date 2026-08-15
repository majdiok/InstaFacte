using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces;

public interface IFirmDecisionTablesService
{
    Task<FirmDecisionTablesDto> GetDecisionTablesAsync(
        Guid firmTenantId,
        CancellationToken cancellationToken = default);
}
