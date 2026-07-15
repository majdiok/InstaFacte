using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class BudgetRepository : IBudgetRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public BudgetRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<BudgetPost>> GetPostsAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.BudgetPosts.AsNoTracking();
        if (!includeInactive)
            query = query.Where(p => p.IsActive);
        return await query.OrderBy(p => p.DisplayOrder).ThenBy(p => p.Code).ToListAsync(cancellationToken);
    }

    public async Task<BudgetPost?> GetPostByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.BudgetPosts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<BudgetPost?> GetPostByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var c = code.Trim().ToUpperInvariant();
        await using var context = _contextFactory.CreateContext();
        return await context.BudgetPosts.FirstOrDefaultAsync(p => p.Code == c, cancellationToken);
    }

    public async Task AddPostAsync(BudgetPost post, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.BudgetPosts.Add(post);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePostAsync(BudgetPost post, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.BudgetPosts.Update(post);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<BudgetYear?> GetYearAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.BudgetYears.AsNoTracking()
            .FirstOrDefaultAsync(y => y.FiscalYear == fiscalYear, cancellationToken);
    }

    public async Task<IReadOnlyList<BudgetLine>> GetLinesAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.BudgetLines.AsNoTracking()
            .Where(l => l.FiscalYear == fiscalYear)
            .ToListAsync(cancellationToken);
    }

    public async Task<Result> UpsertYearLinesAsync(int fiscalYear, BudgetVersion version,
        IReadOnlyList<(Guid PostId, int Month, decimal Amount)> lines, string user,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        var year = await context.BudgetYears
            .FirstOrDefaultAsync(y => y.FiscalYear == fiscalYear, cancellationToken);
        if (year is null)
        {
            var create = BudgetYear.Create(fiscalYear);
            if (create.IsFailure)
                return Result.Failure(create.Error);
            year = create.Value;
            year.SetAuditInfo(user, false);
            context.BudgetYears.Add(year);
        }

        // Cohérence : la version écrite doit être celle que le statut autorise.
        if (year.EditableVersion != version)
            return Result.Failure(Error.Validation("Version",
                year.Status == BudgetYearStatus.Validated
                    ? "Le budget initial est validé : seule la version révisée est modifiable."
                    : "Le budget initial n'est pas encore validé : la version révisée n'est pas modifiable."));

        var postIds = lines.Select(l => l.PostId).Distinct().ToList();
        var existing = await context.BudgetLines
            .Where(l => l.FiscalYear == fiscalYear && l.Version == version && postIds.Contains(l.BudgetPostId))
            .ToListAsync(cancellationToken);
        context.BudgetLines.RemoveRange(existing);

        foreach (var (postId, month, amount) in lines)
        {
            if (amount == 0m)
                continue; // zéro = absence de ligne
            var line = BudgetLine.Create(postId, fiscalYear, version, month, amount);
            if (line.IsFailure)
                return Result.Failure(line.Error);
            line.Value.SetAuditInfo(user, false);
            context.BudgetLines.Add(line.Value);
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ValidateInitialAsync(int fiscalYear, string user, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        var year = await context.BudgetYears
            .FirstOrDefaultAsync(y => y.FiscalYear == fiscalYear, cancellationToken);
        if (year is null)
        {
            var create = BudgetYear.Create(fiscalYear);
            if (create.IsFailure)
                return Result.Failure(create.Error);
            year = create.Value;
            year.SetAuditInfo(user, false);
            context.BudgetYears.Add(year);
        }

        var validate = year.ValidateInitial(user);
        if (validate.IsFailure)
            return validate;
        year.SetAuditInfo(user, true);

        // Point de départ des révisions = copie de l'initial (les révisions éventuelles sont purgées).
        var revised = await context.BudgetLines
            .Where(l => l.FiscalYear == fiscalYear && l.Version == BudgetVersion.Revised)
            .ToListAsync(cancellationToken);
        context.BudgetLines.RemoveRange(revised);

        var initial = await context.BudgetLines.AsNoTracking()
            .Where(l => l.FiscalYear == fiscalYear && l.Version == BudgetVersion.Initial)
            .ToListAsync(cancellationToken);
        foreach (var src in initial)
        {
            var copy = BudgetLine.Create(src.BudgetPostId, fiscalYear, BudgetVersion.Revised, src.Month, src.Amount);
            if (copy.IsFailure)
                return Result.Failure(copy.Error);
            copy.Value.SetAuditInfo(user, false);
            context.BudgetLines.Add(copy.Value);
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
