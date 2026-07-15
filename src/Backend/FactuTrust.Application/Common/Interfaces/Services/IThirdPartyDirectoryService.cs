using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Plan tiers unifié : répertoire clients + fournisseurs (codes auxiliaires, comptes collectifs,
/// soldes) et fiches comptables par tiers.
/// </summary>
public interface IThirdPartyDirectoryService
{
    Task<Result<ThirdPartyDirectoryResultDto>> GetDirectoryAsync(
        ThirdPartyKind? kind, string? search, bool includeInactive, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<ThirdPartyProfileDto>> GetProfileAsync(
        ThirdPartyKind kind, Guid thirdPartyId, CancellationToken cancellationToken = default);

    Task<Result> UpsertProfileAsync(
        ThirdPartyKind kind, Guid thirdPartyId, UpsertThirdPartyProfileRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Génère les codes auxiliaires manquants (C0001…/F0001…) pour les tiers actifs. Idempotent.</summary>
    Task<Result<int>> EnsureAuxiliaryCodesAsync(CancellationToken cancellationToken = default);
}
