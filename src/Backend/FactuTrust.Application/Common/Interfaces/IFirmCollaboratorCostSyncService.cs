using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Orchestre la liaison paie et l'import des coûts employeur cabinet (auto-link email + import paie).
/// </summary>
public interface IFirmCollaboratorCostSyncService
{
    Task<FirmCollaboratorCostSyncResultDto> EnsureFreshAsync(
        Guid firmTenantId,
        int year,
        FirmCostSyncTrigger trigger,
        bool forceImport = false,
        CancellationToken cancellationToken = default);
}
