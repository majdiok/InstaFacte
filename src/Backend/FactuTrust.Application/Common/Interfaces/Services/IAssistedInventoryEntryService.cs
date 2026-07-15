using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Assistant d'écritures d'inventaire de fin d'exercice : génère l'écriture de régularisation
/// et, pour les types périodiques (CCA/PCA/CAP/PAR), son extourne au nouvel exercice — le tout
/// dans une seule transaction. Piloté par <c>AccountingSettings.InventoryAssistantEnabled</c>.
/// </summary>
public interface IAssistedInventoryEntryService
{
    Task<Result<InventoryEntryResultDto>> CreateAsync(CreateInventoryEntryRequest request, CancellationToken cancellationToken = default);
}
