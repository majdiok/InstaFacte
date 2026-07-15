using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class JournalEntryTemplateRepository : IJournalEntryTemplateRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public JournalEntryTemplateRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<JournalEntryTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.JournalEntryTemplates
            .AsNoTracking()
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<JournalEntryTemplate>> GetAllAsync(
        bool? activeOnly = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.JournalEntryTemplates.AsNoTracking();

        if (activeOnly == true)
            query = query.Where(t => t.IsActive);
        else if (activeOnly == false)
            query = query.Where(t => !t.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(t =>
                t.Name.Contains(s) ||
                (t.Description != null && t.Description.Contains(s)) ||
                t.JournalCode.Contains(s));
        }

        return await query
            .Include(t => t.Lines)
            .AsSplitQuery()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        var n = name.Trim();
        await using var context = _contextFactory.CreateContext();
        var query = context.JournalEntryTemplates.Where(t => t.Name == n);
        if (excludeId.HasValue)
            query = query.Where(t => t.Id != excludeId.Value);
        return await query.AnyAsync(cancellationToken);
    }

    public async Task<JournalEntryTemplate> AddAsync(JournalEntryTemplate entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.JournalEntryTemplates.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(JournalEntryTemplate entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.JournalEntryTemplates.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(JournalEntryTemplate entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        // Soft delete via Deactivate, but allow hard delete on request
        context.JournalEntryTemplates.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task IncrementUsageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var entity = await context.JournalEntryTemplates
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (entity is null) return;
        entity.IncrementUsage();
        await context.SaveChangesAsync(cancellationToken);
    }
}
