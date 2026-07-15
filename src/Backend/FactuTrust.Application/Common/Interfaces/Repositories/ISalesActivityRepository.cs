using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ISalesActivityRepository
{
    Task<SalesActivity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<SalesActivity> Items, int TotalCount)> SearchAsync(
        Guid? clientId,
        Guid? assignedUserId,
        Guid? opportunityId,
        bool? completed,
        DateTime? dueFrom,
        DateTime? dueTo,
        int? activityType,
        string? searchSubject,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregated totals over the ENTIRE filtered set (same filters as <see cref="SearchAsync"/>,
    /// without pagination). Powers the activity list "totals zone".
    /// </summary>
    Task<ActivityListSummaryDto> GetSummaryAsync(
        Guid? clientId,
        Guid? assignedUserId,
        Guid? opportunityId,
        bool? completed,
        DateTime? dueFrom,
        DateTime? dueTo,
        int? activityType,
        string? searchSubject,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesActivity>> GetRemindersAsync(Guid assignedUserId, DateTime upTo, CancellationToken cancellationToken = default);
    Task<SalesActivity> AddAsync(SalesActivity entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(SalesActivity entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(SalesActivity entity, CancellationToken cancellationToken = default);
}
