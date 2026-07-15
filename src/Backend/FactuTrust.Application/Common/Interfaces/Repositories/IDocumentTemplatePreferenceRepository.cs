using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IDocumentTemplatePreferenceRepository
{
    Task<IReadOnlyList<DocumentTemplatePreference>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<DocumentTemplatePreference?> GetByTypeAsync(Guid tenantId, PrintableDocumentType documentType, CancellationToken cancellationToken = default);
    Task AddAsync(DocumentTemplatePreference preference, CancellationToken cancellationToken = default);
    Task UpdateAsync(DocumentTemplatePreference preference, CancellationToken cancellationToken = default);
}
