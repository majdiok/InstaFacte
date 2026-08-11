using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FirmPayrollLinkSource = FactuTrust.Domain.Entities.FirmGovernance.FirmPayrollLinkSource;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Gère le coût employeur annuel des collaborateurs et leur taux horaire de revient.
/// </summary>
/// <remarks>
/// Point d'entrée unique de tout ce qui coûte par collaborateur et par exercice. La rentabilité
/// par dossier comme la rentabilité collaborateur y puisent, ce qui garantit qu'elles ne peuvent
/// pas afficher deux coûts différents pour le même collaborateur.
/// </remarks>
public interface IFirmCollaboratorCostService
{
    Task<IReadOnlyList<FirmCollaboratorYearCostDto>> ListAsync(
        Guid firmTenantId,
        int year,
        CancellationToken cancellationToken = default);

    Task<Result<FirmCollaboratorYearCostDto>> SaveAsync(
        Guid firmTenantId,
        bool isManager,
        Guid collaboratorUserId,
        int year,
        SaveFirmCollaboratorYearCostDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reporte les coûts de la paie du cabinet sur les collaborateurs qui y sont explicitement liés.
    /// </summary>
    Task<Result<FirmPayrollImportResultDto>> ImportFromPayrollAsync(
        Guid firmTenantId,
        bool isManager,
        int year,
        bool forceOverwriteManual = false,
        bool staleOrMissingOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>Salariés de la paie du cabinet, pour alimenter le sélecteur de liaison.</summary>
    Task<FirmPayrollCostSnapshotDto> GetPayrollEmployeesAsync(
        Guid firmTenantId,
        int year,
        CancellationToken cancellationToken = default);

    Task<Result> LinkPayrollEmployeeAsync(
        Guid firmTenantId,
        bool isManager,
        Guid collaboratorUserId,
        Guid? payrollEmployeeId,
        FirmPayrollLinkSource linkSourceWhenSet = FirmPayrollLinkSource.Manual,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Établit plusieurs liaisons collaborateur ↔ salarié en une seule écriture.
    /// </summary>
    /// <remarks>
    /// Nécessaire au provisionnement : lier un à un enregistrait dans Master au fil de la boucle,
    /// alors que les salariés correspondants n'étaient persistés dans la base de paie qu'à la fin.
    /// Un échec de ce dernier enregistrement laissait des profils pointant vers des salariés
    /// inexistants. Ici, rien n'est écrit tant que toutes les liaisons ne sont pas validées.
    /// </remarks>
    Task<Result<FirmPayrollLinkBatchResultDto>> LinkPayrollEmployeesAsync(
        Guid firmTenantId,
        bool isManager,
        IReadOnlyList<FirmPayrollEmployeeLinkRequest> links,
        FirmPayrollLinkSource linkSource,
        CancellationToken cancellationToken = default);
}
