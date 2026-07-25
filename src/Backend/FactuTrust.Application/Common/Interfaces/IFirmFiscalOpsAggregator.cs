using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces;

public interface IFirmFiscalOpsAggregator
{
    Task<FirmFiscalOpsSummaryDto> AggregateAsync(
        IReadOnlyList<Guid> companyTenantIds,
        CancellationToken cancellationToken = default);
}
