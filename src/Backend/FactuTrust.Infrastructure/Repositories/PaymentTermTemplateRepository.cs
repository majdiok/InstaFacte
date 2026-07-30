using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Pricing;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

/// <summary>Repository des conditions de règlement structurées.</summary>
public sealed class PaymentTermTemplateRepository : IPaymentTermTemplateRepository
{
    private readonly TenantDbContextFactory _contextFactory;

    public PaymentTermTemplateRepository(TenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PaymentTermTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PaymentTermTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentTermTemplate>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PaymentTermTemplates
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.DelayDays)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<PaymentTermTemplate?> GetDefaultAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PaymentTermTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IsDefault, cancellationToken);
    }

    public async Task ClearDefaultAsync(Guid exceptId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        var others = await context.PaymentTermTemplates
            .Where(t => t.IsDefault && t.Id != exceptId)
            .ToListAsync(cancellationToken);

        if (others.Count == 0)
            return;

        foreach (var other in others)
            other.ClearDefault();

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentTermTemplate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PaymentTermTemplates
            .AsNoTracking()
            .OrderBy(t => t.DelayDays)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<PaymentTermTemplate> AddAsync(PaymentTermTemplate entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PaymentTermTemplates.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(PaymentTermTemplate entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PaymentTermTemplates.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PaymentTermTemplate entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PaymentTermTemplates.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PaymentTermTemplates.AnyAsync(t => t.Id == id, cancellationToken);
    }
}
