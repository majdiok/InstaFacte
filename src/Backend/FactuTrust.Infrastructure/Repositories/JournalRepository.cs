using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class JournalRepository : IJournalRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public JournalRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Journal>> GetAllJournalsAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.Journals.AsNoTracking();
        if (!includeInactive)
            query = query.Where(j => j.IsActive);
        return await query.OrderBy(j => j.Code).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JournalFamily>> GetAllFamiliesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.JournalFamilies.AsNoTracking().OrderBy(f => f.Label).ToListAsync(cancellationToken);
    }

    public async Task<Journal?> GetJournalByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Journals.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    public async Task<Journal?> GetJournalByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var c = code.Trim().ToUpperInvariant();
        await using var context = _contextFactory.CreateContext();
        return await context.Journals.FirstOrDefaultAsync(j => j.Code == c, cancellationToken);
    }

    public async Task<bool> ExistsActiveJournalCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var c = code.Trim().ToUpperInvariant();
        await using var context = _contextFactory.CreateContext();
        return await context.Journals.AnyAsync(j => j.Code == c && j.IsActive, cancellationToken);
    }

    public async Task AddJournalAsync(Journal journal, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Journals.Add(journal);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateJournalAsync(Journal journal, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Journals.Update(journal);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddFamilyAsync(JournalFamily family, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.JournalFamilies.Add(family);
        await context.SaveChangesAsync(cancellationToken);
    }
}
