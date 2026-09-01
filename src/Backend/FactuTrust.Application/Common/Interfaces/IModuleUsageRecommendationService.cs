using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Plan §3.3 — explainable, rule-based module usage recommendations for the current tenant
/// (ambient tenant/user resolved internally from <see cref="ITenantContext"/>/<see cref="ICurrentUser"/>,
/// mirroring <c>IProductOnboardingService</c>). A dismissed recommendation never resurfaces for
/// that tenant.
/// </summary>
public interface IModuleUsageRecommendationService
{
    Task<IReadOnlyList<ModuleRecommendationDto>> GetRecommendationsAsync(CancellationToken cancellationToken);

    Task<Result<bool>> DismissAsync(int moduleId, CancellationToken cancellationToken);
}
