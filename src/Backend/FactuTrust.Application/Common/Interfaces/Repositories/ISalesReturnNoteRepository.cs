using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ISalesReturnNoteRepository : IRepository<SalesReturnNote>
{
    Task<SalesReturnNote?> GetByIdWithLinesAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SalesReturnNote?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<SalesReturnNote> Items, int TotalCount)> SearchAsync(
        string? searchTerm,
        SalesReturnNoteStatus? status,
        Guid? clientId,
        Guid? deliveryNoteId,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<SalesReturnNoteListSummaryDto> GetSummaryAsync(
        string? searchTerm,
        SalesReturnNoteStatus? status,
        Guid? clientId,
        Guid? deliveryNoteId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesReturnNote>> GetByDeliveryNoteIdAsync(
        Guid deliveryNoteId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsNumberAsync(string number, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a draft in a tracked context so <c>Version</c> concurrency uses the store token.
    /// </summary>
    Task<Result> ConfirmPersistedAsync(Guid id, string updatedBy, CancellationToken cancellationToken = default);
}
