using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class SalesActivityRepository : ISalesActivityRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public SalesActivityRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<SalesActivity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.SalesActivities
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<SalesActivity> Items, int TotalCount)> SearchAsync(
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
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        await using var context = _contextFactory.CreateContext();
        var query = ApplyActivityFilters(
            context.SalesActivities.AsNoTracking(),
            clientId, assignedUserId, opportunityId, completed, dueFrom, dueTo, activityType, searchSubject);

        query = query
            .OrderByDescending(a => a.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(a => a.CreatedAt);

        var total = await query.CountAsync(cancellationToken);
        var skip = (page - 1) * pageSize;
        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <summary>
    /// Applies the activity list filters. Single source of truth shared by <see cref="SearchAsync"/>
    /// and <see cref="GetSummaryAsync"/> so the list and its totals zone can never diverge.
    /// </summary>
    private static IQueryable<SalesActivity> ApplyActivityFilters(
        IQueryable<SalesActivity> query,
        Guid? clientId,
        Guid? assignedUserId,
        Guid? opportunityId,
        bool? completed,
        DateTime? dueFrom,
        DateTime? dueTo,
        int? activityType,
        string? searchSubject)
    {
        if (clientId.HasValue)
            query = query.Where(a => a.ClientId == clientId.Value);

        if (assignedUserId.HasValue)
            query = query.Where(a => a.AssignedUserId == assignedUserId.Value);

        if (opportunityId.HasValue)
            query = query.Where(a => a.OpportunityId == opportunityId.Value);

        if (completed == true)
            query = query.Where(a => a.CompletedAt != null);
        else if (completed == false)
            query = query.Where(a => a.CompletedAt == null);

        if (dueFrom.HasValue)
        {
            var d = dueFrom.Value.Date;
            query = query.Where(a => a.DueDate.HasValue && a.DueDate >= d);
        }

        if (dueTo.HasValue)
        {
            var d = dueTo.Value.Date;
            query = query.Where(a => a.DueDate.HasValue && a.DueDate <= d);
        }

        if (activityType.HasValue && Enum.IsDefined(typeof(ActivityType), activityType.Value))
            query = query.Where(a => (int)a.Type == activityType.Value);

        if (!string.IsNullOrWhiteSpace(searchSubject))
        {
            var term = searchSubject.Trim();
            query = query.Where(a => a.Subject.Contains(term));
        }

        return query;
    }

    public async Task<ActivityListSummaryDto> GetSummaryAsync(
        Guid? clientId,
        Guid? assignedUserId,
        Guid? opportunityId,
        bool? completed,
        DateTime? dueFrom,
        DateTime? dueTo,
        int? activityType,
        string? searchSubject,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        var filtered = ApplyActivityFilters(
            context.SalesActivities.AsNoTracking(),
            clientId, assignedUserId, opportunityId, completed, dueFrom, dueTo, activityType, searchSubject);

        var rows = await filtered
            .Select(a => new { a.CompletedAt, a.DueDate })
            .ToListAsync(cancellationToken);

        var today = DateTime.UtcNow.Date;

        return new ActivityListSummaryDto
        {
            Count = rows.Count,
            OpenCount = rows.Count(r => r.CompletedAt == null),
            CompletedCount = rows.Count(r => r.CompletedAt != null),
            OverdueCount = rows.Count(r => r.CompletedAt == null && r.DueDate.HasValue && r.DueDate.Value.Date < today)
        };
    }

    public async Task<IReadOnlyList<SalesActivity>> GetRemindersAsync(
        Guid assignedUserId,
        DateTime upTo,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.SalesActivities
            .AsNoTracking()
            .Where(a =>
                a.AssignedUserId == assignedUserId
                && a.DueDate != null
                && a.DueDate <= upTo
                && a.CompletedAt == null)
            .OrderBy(a => a.DueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<SalesActivity> AddAsync(SalesActivity entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.SalesActivities.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(SalesActivity entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.SalesActivities.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(SalesActivity entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.SalesActivities.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
