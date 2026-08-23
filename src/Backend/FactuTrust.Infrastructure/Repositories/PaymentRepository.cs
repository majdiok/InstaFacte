using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Payment aggregate.
/// </summary>
public sealed class PaymentRepository : IPaymentRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public PaymentRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Payments
            .Include(p => p.Invoice)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Payments
            .Where(p => p.InvoiceId == invoiceId)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetTotalPaidByInvoiceIdsAsync(
        IEnumerable<Guid> invoiceIds,
        CancellationToken cancellationToken = default)
    {
        var ids = invoiceIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, decimal>();

        await using var context = _contextFactory.CreateContext();
        var totals = await context.Payments
            .Where(p => ids.Contains(p.InvoiceId) && !p.IsRefunded)
            .GroupBy(p => p.InvoiceId)
            .Select(g => new
            {
                InvoiceId = g.Key,
                Total = g.Sum(p => p.Amount.Amount + (p.ClientWithholdingAmount ?? 0m))
            })
            .ToListAsync(cancellationToken);

        return totals.ToDictionary(x => x.InvoiceId, x => x.Total);
    }

    public async Task<IReadOnlyList<Payment>> GetByDateRangeAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var from = fromDate.Date;
        var to = toDate.Date;
        return await context.Payments
            .Include(p => p.Invoice)
            .ThenInclude(i => i!.Client)
            .Where(p => !p.IsRefunded && p.PaymentDate >= from && p.PaymentDate <= to)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClientPaymentReportSourceDto>> GetClientPaymentReportSourcesAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var from = fromDate.Date;
        var to = toDate.Date;

        return await context.Payments
            .AsNoTracking()
            .Where(p => !p.IsRefunded && p.PaymentDate >= from && p.PaymentDate <= to)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .Select(p => new ClientPaymentReportSourceDto
            {
                PaymentId = p.Id,
                PaymentDate = p.PaymentDate,
                ClientName = p.Invoice != null && p.Invoice.Client != null ? p.Invoice.Client.Name : string.Empty,
                InvoiceId = p.InvoiceId,
                InvoiceNumber = p.Invoice != null ? EF.Property<string>(p.Invoice.Number, "Value") : string.Empty,
                Amount = p.Amount.Amount,
                Currency = p.Amount.Currency,
                Method = (int)p.Method,
                Reference = p.Reference
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Payments
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(int Month, decimal TotalSubie, int PaymentCount)>> GetClientWithholdingAggregatesByYearAsync(
        int year,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var rows = await context.Payments.AsNoTracking()
            .Where(p => !p.IsRefunded && p.ClientWithholdingAmount != null && p.ClientWithholdingAmount > 0
                && p.PaymentDate.Year == year)
            .GroupBy(p => p.PaymentDate.Month)
            .Select(g => new
            {
                Month = g.Key,
                TotalSubie = g.Sum(p => p.ClientWithholdingAmount!.Value),
                Count = g.Count()
            })
            .OrderBy(x => x.Month)
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.Month, r.TotalSubie, r.Count)).ToList();
    }

    public async Task<IReadOnlyList<ClientWithholdingReportRowDto>> GetClientWithholdingReportRowsAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var from = fromDate.Date;
        var to = toDate.Date;

        return await context.Payments
            .AsNoTracking()
            .Where(p => !p.IsRefunded
                && p.ClientWithholdingAmount != null && p.ClientWithholdingAmount > 0
                && p.PaymentDate >= from && p.PaymentDate <= to
                && p.Invoice != null)
            .GroupBy(p => new
            {
                p.Invoice!.ClientId,
                ClientName = p.Invoice.Client != null ? p.Invoice.Client.Name : string.Empty,
                Currency = p.Amount.Currency
            })
            .Select(g => new ClientWithholdingReportRowDto
            {
                ClientId = g.Key.ClientId,
                ClientName = g.Key.ClientName,
                PaymentCount = g.Count(),
                TotalWithholding = g.Sum(p => p.ClientWithholdingAmount!.Value),
                Currency = g.Key.Currency
            })
            .OrderBy(r => r.ClientName)
            .ToListAsync(cancellationToken);
    }

    public async Task<Payment> AddAsync(Payment entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        // Attach the Invoice and its related entities (Client, Lines, Products) as Unchanged
        // so EF Core doesn't try to insert them. The Payment was created with an Invoice
        // loaded from another context (GetByIdWithLinesAsync); without this, EF would treat
        // the entire graph as new and attempt INSERTs, causing PK violations.
        if (entity.Invoice != null)
        {
            context.Attach(entity.Invoice);
        }

        context.Payments.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(Payment entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Payments.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Payment entity, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.Payments.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Payments.AnyAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetByCashRegisterSessionIdAsync(
        Guid cashRegisterSessionId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.Payments
            .Include(p => p.Invoice)
            .Where(p => p.CashRegisterSessionId == cashRegisterSessionId)
            .OrderBy(p => p.PaymentDate)
            .ThenBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
