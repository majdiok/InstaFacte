using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Rentabilité collaborateur = CA − MS − MS admin − Charges IT − Charges exploitation (Décisiel).
/// Snapshot annuel persisté avec préremplissage auto.
/// </summary>
public interface IFirmCollaboratorRentabilityService
{
    Task<FirmCollaboratorRentabilityListDto> ListAsync(
        Guid firmTenantId,
        int? year,
        Guid? collaboratorUserId,
        CancellationToken cancellationToken = default);

    Task<FirmCollaboratorRentabilityDetailDto?> GetByIdAsync(
        Guid firmTenantId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<FirmCollaboratorRentabilityDetailDto>> GetPrefillAsync(
        Guid firmTenantId,
        Guid collaboratorUserId,
        int year,
        CancellationToken cancellationToken = default);

    Task<Result<FirmCollaboratorRentabilityDetailDto>> SaveAsync(
        Guid firmTenantId,
        SaveFirmCollaboratorRentabilityDto dto,
        Guid? existingId,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid firmTenantId, Guid id, CancellationToken cancellationToken = default);

    Task<Result<DuplicateFirmRentabilityResultDto>> DuplicateAsync(
        Guid firmTenantId,
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recalcule toutes les marges enregistrées selon le modèle de marge sur coût direct.
    /// </summary>
    /// <remarks>
    /// Les exercices dépourvus de feuilles de temps sont laissés intacts et remontés dans le compte
    /// rendu : les recalculer ramènerait leur chiffre d'affaires à zéro.
    /// </remarks>
    Task<Result<FirmRentabilityRecalculationResultDto>> RecalculateAllAsync(
        Guid firmTenantId,
        bool isManager,
        CancellationToken cancellationToken = default);
}
