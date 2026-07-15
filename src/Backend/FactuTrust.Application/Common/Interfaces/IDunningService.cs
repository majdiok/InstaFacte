using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>Lot C6 — CRUD campagnes dunning + activation.</summary>
public interface IDunningCampaignService
{
    Task<DunningCampaignsListDto> ListAsync(CancellationToken cancellationToken = default);

    Task<Result<DunningCampaignDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<DunningCampaignDto>> CreateAsync(CreateDunningCampaignRequest request, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<Result<DunningCampaignDto>> UpdateAsync(Guid id, UpdateDunningCampaignRequest request, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<Result> ActivateAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<Result> DeactivateAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Garantit qu'au moins une campagne par défaut existe et est active. Idempotent.</summary>
    Task<Result> EnsureDefaultExistsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Lot C6 — Lecture paginée des états dunning + extension grace + renouvellement manuel.</summary>
public interface IDunningStateQueryService
{
    Task<DunningStatesPageDto> ListAsync(string? outcome, Guid? tenantId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Result<DunningStateDto>> GetBySubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
}

/// <summary>Lot C6 — Renouvellement manuel + extension grace period.</summary>
public interface ISubscriptionRenewalService
{
    /// <summary>Renouvelle immédiatement un abonnement (génère facture + applique extension EndDate).</summary>
    Task<Result> RenewNowAsync(Guid subscriptionId, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Étend la période de grâce de N jours (repousse <c>NextActionAt</c> du DunningState).</summary>
    Task<Result> ExtendGraceAsync(Guid subscriptionId, int days, Guid actorUserId, CancellationToken cancellationToken = default);
}
