using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>Lot C3 — CRUD coupons côté admin plateforme.</summary>
public interface ICouponAdminService
{
    Task<CouponsPageDto> ListAsync(bool? activeOnly, string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Result<CouponDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<CouponDto>> CreateAsync(CreateCouponRequest request, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<Result<CouponDto>> UpdateAsync(Guid id, UpdateCouponRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeactivateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result> ReactivateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CouponRedemptionDto>> ListRedemptionsAsync(Guid couponId, CancellationToken cancellationToken = default);
}

/// <summary>Lot C3 — CRUD crédits tenant côté admin plateforme.</summary>
public interface ITenantCreditAdminService
{
    Task<TenantCreditsPageDto> ListByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<TenantCreditsPageDto> ListAllAsync(bool? activeOnly, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Result<TenantCreditDto>> GrantAsync(Guid tenantId, GrantTenantCreditRequest request, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<Result> RevokeAsync(Guid creditId, RevokeTenantCreditRequest request, CancellationToken cancellationToken = default);
}
