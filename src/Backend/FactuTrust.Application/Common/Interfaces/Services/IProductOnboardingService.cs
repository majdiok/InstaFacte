using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IProductOnboardingService
{
    Task<ProductOnboardingDto> GetMineAsync(CancellationToken cancellationToken);

    Task<Result<ProductOnboardingDto>> PatchMineAsync(
        PatchProductOnboardingRequest request,
        CancellationToken cancellationToken);
}
