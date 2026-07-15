using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Forecasting.Dtos;

namespace FactuTrust.Application.Common.Interfaces.Forecasting;

/// <summary>
/// Generates promotion / discount recommendations from history + stock + ABC/XYZ + Tunisian calendar.
/// Recommendations are inert — actual discount activation is a separate user-confirmed step.
/// </summary>
public interface IPromotionRecommendationService
{
    /// <summary>Generate fresh promotion recommendations for the next horizon (default 30 days).</summary>
    Task<int> GenerateAsync(int horizonDays, CancellationToken ct = default);

    Task<PagedResult<PromotionRecommendationDto>> GetActiveAsync(
        Guid? productId,
        Guid? categoryId,
        DateTime? periodStart,
        DateTime? periodEnd,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<PreparePromotionDraftResultDto> PrepareDiscountDraftAsync(
        Guid promotionRecommendationId,
        CancellationToken ct = default);

    Task DismissAsync(Guid recommendationId, CancellationToken ct = default);

    Task<PromotionSimulationResultDto> SimulateAsync(
        Guid productId,
        decimal discountPercent,
        int durationDays,
        CancellationToken ct = default);
}
