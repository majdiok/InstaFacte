using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IQuoteTemplateRepository
{
    Task<QuoteTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuoteTemplate>> GetAllAsync(
        bool? activeOnly = null,
        string? search = null,
        CancellationToken cancellationToken = default);
    Task<QuoteTemplate> AddAsync(QuoteTemplate entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(QuoteTemplate entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(QuoteTemplate entity, CancellationToken cancellationToken = default);
    Task<Result> UpdateContentAsync(Guid id, UpdateQuoteTemplateRequest request, string updatedBy, CancellationToken cancellationToken = default);
    Task<Result> SetActiveAsync(Guid id, bool isActive, string updatedBy, CancellationToken cancellationToken = default);
    Task<Result> IncrementUsageAsync(Guid id, CancellationToken cancellationToken = default);
}
