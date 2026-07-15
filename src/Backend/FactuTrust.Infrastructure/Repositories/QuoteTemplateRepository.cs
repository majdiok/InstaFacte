using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class QuoteTemplateRepository : IQuoteTemplateRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public QuoteTemplateRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<QuoteTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.QuoteTemplates
            .AsNoTracking()
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<QuoteTemplate>> GetAllAsync(
        bool? activeOnly = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.QuoteTemplates.AsNoTracking();

        if (activeOnly == true)
            query = query.Where(t => t.IsActive);
        else if (activeOnly == false)
            query = query.Where(t => !t.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(t =>
                t.Name.Contains(s) ||
                (t.Description != null && t.Description.Contains(s)));
        }

        return await query
            .Include(t => t.Lines)
            .AsSplitQuery()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<QuoteTemplate> AddAsync(QuoteTemplate entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.QuoteTemplates.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(QuoteTemplate entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.QuoteTemplates.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(QuoteTemplate entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.QuoteTemplates.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Result> UpdateContentAsync(
        Guid id,
        UpdateQuoteTemplateRequest request,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        await using var strategyContext = _contextFactory.CreateContext();
        var strategy = strategyContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var context = _contextFactory.CreateContext();
            var entity = await context.QuoteTemplates
                .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

            if (entity is null)
                return Result.Failure(Error.Validation("Id", "Modèle introuvable"));

            var u = entity.Update(
                request.Name,
                request.Description,
                request.DefaultNotes,
                request.DefaultTermsAndConditions,
                request.DefaultValidityDays);
            if (u.IsFailure)
                return u;

            if (request.IsActive.HasValue)
            {
                if (request.IsActive.Value)
                    entity.Reactivate();
                else
                    entity.Deactivate();
            }

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await context.QuoteTemplateLines
                    .Where(l => l.QuoteTemplateId == id)
                    .ExecuteDeleteAsync(cancellationToken);

                entity.ClearLines();

                int sort = 0;
                foreach (var line in request.Lines)
                {
                    var tl = QuoteTemplateLine.Create(
                        entity,
                        line.ProductId,
                        line.Quantity,
                        line.SortOrder > 0 ? line.SortOrder : ++sort,
                        line.CustomUnitPrice.HasValue ? Money.Create(line.CustomUnitPrice.Value) : null,
                        line.DiscountPercent);
                    entity.AddLine(tl);
                }

                entity.SetAuditInfo(updatedBy, true);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return Result.Success();
        });
    }

    public async Task<Result> SetActiveAsync(
        Guid id,
        bool isActive,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var entity = await context.QuoteTemplates
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.Validation("Id", "Modèle introuvable"));

        if (isActive)
            entity.Reactivate();
        else
            entity.Deactivate();

        entity.SetAuditInfo(updatedBy, true);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> IncrementUsageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var entity = await context.QuoteTemplates
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.Validation("Id", "Modèle introuvable"));

        entity.IncrementUsage();
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
