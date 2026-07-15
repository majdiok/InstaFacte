using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>Lot C2 — Lecture / management de la table <c>EmailMessages</c> côté backoffice.</summary>
public interface IEmailMessageQueryService
{
    Task<EmailMessagesPageDto> ListAsync(
        Guid? tenantId,
        int? statusFilter,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<EmailMessageDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Re-met une ligne en file Queued pour un nouvel essai (failed → queued).</summary>
    Task<Result> RequeueAsync(Guid id, CancellationToken cancellationToken = default);
}
