using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

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
        CancellationToken cancellationToken = default);
}
