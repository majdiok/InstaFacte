using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface INumberingSchemeRepository
{
    Task<IReadOnlyList<DocumentNumberingScheme>> GetAllForYearAsync(Guid tenantId, int fiscalYear, CancellationToken cancellationToken = default);
    Task<DocumentNumberingScheme?> GetByTypeAsync(Guid tenantId, NumberingDocumentType documentType, int fiscalYear, CancellationToken cancellationToken = default);
    Task AddAsync(DocumentNumberingScheme scheme, CancellationToken cancellationToken = default);
    Task UpdateAsync(DocumentNumberingScheme scheme, CancellationToken cancellationToken = default);
    Task<bool> HasDocumentsAsync(Guid tenantId, NumberingDocumentType documentType, int fiscalYear, CancellationToken cancellationToken = default);
}