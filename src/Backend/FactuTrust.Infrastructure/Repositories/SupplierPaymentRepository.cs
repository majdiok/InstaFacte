using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SupplierPayment aggregate.
/// </summary>
public sealed class SupplierPaymentRepository : ISupplierPaymentRepository
{
    private readonly TenantDbContextFactory _contextFactory;

    public SupplierPaymentRepository(TenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<SupplierPayment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.SupplierPayments
            .Include(p => p.SupplierInvoice)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SupplierPayment>> GetByIdsWithInvoiceAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return Array.Empty<SupplierPayment>();

        await using var context = _contextFactory.CreateContext();
        return await context.SupplierPayments
            .AsNoTracking()
            .Include(p => p.SupplierInvoice)
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SupplierPayment>> GetBySupplierInvoiceIdAsync(Guid supplierInvoiceId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.SupplierPayments
            .Where(p => p.SupplierInvoiceId == supplierInvoiceId)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SupplierPayment>> GetByDateRangeAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var from = fromDate.Date;
        var to = toDate.Date;
        return await context.SupplierPayments
            .Include(p => p.SupplierInvoice)
            .ThenInclude(si => si!.Supplier)
            .Where(p => p.PaymentDate >= from && p.PaymentDate <= to)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetTotalPaidBySupplierInvoiceIdsAsync(
        IEnumerable<Guid> supplierInvoiceIds,
        CancellationToken cancellationToken = default)
    {
        var ids = supplierInvoiceIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, decimal>();

        await using var context = _contextFactory.CreateContext();
        var totals = await context.SupplierPayments
            .Where(p => ids.Contains(p.SupplierInvoiceId))
            .GroupBy(p => p.SupplierInvoiceId)
            .Select(g => new { SupplierInvoiceId = g.Key, Total = g.Sum(p => p.Amount.Amount) })
            .ToListAsync(cancellationToken);

        return totals.ToDictionary(x => x.SupplierInvoiceId, x => x.Total);
    }

    public async Task<IReadOnlyList<SupplierPayment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.SupplierPayments
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<SupplierPayment> AddAsync(SupplierPayment entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        if (entity.SupplierInvoice != null)
        {
            context.Attach(entity.SupplierInvoice);
        }

        context.SupplierPayments.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(SupplierPayment entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.SupplierPayments.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(SupplierPayment entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.SupplierPayments.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.SupplierPayments.AnyAsync(p => p.Id == id, cancellationToken);
    }
}
