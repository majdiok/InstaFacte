using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class FiscalScheduleRepository : IFiscalScheduleRepository
{
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly TimeProvider _timeProvider;

    public FiscalScheduleRepository(ITenantDbContextFactory contextFactory, TimeProvider timeProvider)
    {
        _contextFactory = contextFactory;
        _timeProvider = timeProvider;
    }

    public async Task<FiscalScheduleQueryResult> ListAsync(FiscalScheduleQueryCriteria criteria, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        var query = ctx.FiscalScheduleEntries
            .AsNoTracking()
            .Include(e => e.Attachments)
            .Include(e => e.History)
            .AsQueryable();

        query = ApplyBaseFilters(query, criteria);

        var rows = await query
            .OrderBy(e => e.DueDate)
            .ThenBy(e => e.ObligationType)
            .ToListAsync(cancellationToken);

        var today = _timeProvider.GetLocalNow().DateTime.Date;
        if (criteria.Status.HasValue)
            rows = rows.Where(e => e.ResolveStatus(today) == criteria.Status.Value).ToList();

        var page = Math.Max(criteria.Page, 1);
        var pageSize = Math.Clamp(criteria.PageSize, 1, 200);
        var items = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new FiscalScheduleQueryResult(items, rows, rows.Count);
    }

    public async Task<FiscalScheduleEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        return await ctx.FiscalScheduleEntries
            .Include(e => e.Attachments)
            .Include(e => e.History)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        FiscalObligationType obligationType,
        int fiscalYear,
        int? periodMonth,
        int? periodQuarter,
        FiscalScheduleSourceType sourceType,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        return await ctx.FiscalScheduleEntries.AsNoTracking()
            .AnyAsync(e =>
                !e.IsCancelled &&
                e.ObligationType == obligationType &&
                e.FiscalYear == fiscalYear &&
                e.PeriodMonth == periodMonth &&
                e.PeriodQuarter == periodQuarter &&
                e.SourceType == sourceType,
                cancellationToken);
    }

    public async Task<FiscalScheduleEntry?> GetMonthlyDeclarationEntryAsync(
        int fiscalYear, int periodMonth, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        return await ctx.FiscalScheduleEntries
            .Where(e =>
                !e.IsCancelled &&
                e.ObligationType == FiscalObligationType.MonthlyDeclaration &&
                e.FiscalYear == fiscalYear &&
                e.PeriodMonth == periodMonth)
            .OrderBy(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(FiscalScheduleEntry entry, FiscalScheduleHistoryEntry? history = null, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        ctx.FiscalScheduleEntries.Add(entry);
        if (history is not null)
            ctx.FiscalScheduleHistoryEntries.Add(history);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(FiscalScheduleEntry entry, FiscalScheduleHistoryEntry? history = null, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        ctx.FiscalScheduleEntries.Update(entry);
        if (history is not null)
            ctx.FiscalScheduleHistoryEntries.Add(history);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FiscalScheduleHistoryEntry>> GetHistoryAsync(Guid fiscalScheduleEntryId, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        return await ctx.FiscalScheduleHistoryEntries.AsNoTracking()
            .Where(h => h.FiscalScheduleEntryId == fiscalScheduleEntryId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FiscalScheduleAttachment>> GetAttachmentsAsync(Guid fiscalScheduleEntryId, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        return await ctx.FiscalScheduleAttachments.AsNoTracking()
            .Where(a => a.FiscalScheduleEntryId == fiscalScheduleEntryId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<FiscalScheduleAttachment?> GetAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        return await ctx.FiscalScheduleAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);
    }

    public async Task AddAttachmentAsync(FiscalScheduleAttachment attachment, FiscalScheduleHistoryEntry? history = null, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        ctx.FiscalScheduleAttachments.Add(attachment);
        if (history is not null)
            ctx.FiscalScheduleHistoryEntries.Add(history);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAttachmentAsync(FiscalScheduleAttachment attachment, FiscalScheduleHistoryEntry? history = null, CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();
        ctx.FiscalScheduleAttachments.Remove(attachment);
        if (history is not null)
            ctx.FiscalScheduleHistoryEntries.Add(history);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<FiscalScheduleEntry> ApplyBaseFilters(
        IQueryable<FiscalScheduleEntry> query,
        FiscalScheduleQueryCriteria criteria)
    {
        if (!criteria.IncludeCancelled)
            query = query.Where(e => !e.IsCancelled);
        if (criteria.FiscalYear.HasValue)
            query = query.Where(e => e.FiscalYear == criteria.FiscalYear.Value);
        if (criteria.PeriodMonth.HasValue)
            query = query.Where(e => e.PeriodMonth == criteria.PeriodMonth.Value);
        if (criteria.PeriodQuarter.HasValue)
            query = query.Where(e => e.PeriodQuarter == criteria.PeriodQuarter.Value);
        if (criteria.ObligationType.HasValue)
            query = query.Where(e => e.ObligationType == criteria.ObligationType.Value);
        if (criteria.ResponsibleUserId.HasValue)
            query = query.Where(e => e.ResponsibleUserId == criteria.ResponsibleUserId.Value);
        if (criteria.DueFrom.HasValue)
            query = query.Where(e => e.DueDate >= criteria.DueFrom.Value.Date);
        if (criteria.DueTo.HasValue)
            query = query.Where(e => e.DueDate <= criteria.DueTo.Value.Date);
        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var search = criteria.Search.Trim();
            query = query.Where(e =>
                e.ObligationLabel.Contains(search) ||
                (e.ResponsibleName != null && e.ResponsibleName.Contains(search)) ||
                (e.Observations != null && e.Observations.Contains(search)));
        }

        return query;
    }
}
