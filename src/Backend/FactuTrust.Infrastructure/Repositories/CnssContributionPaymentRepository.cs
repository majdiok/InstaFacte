using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class CnssContributionPaymentRepository : ICnssContributionPaymentRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public CnssContributionPaymentRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<CnssContributionPayment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CnssContributionPayments
            .Include(p => p.PayrollRun)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<CnssContributionPayment?> GetActiveByPeriodAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CnssContributionPayments
            .AsNoTracking()
            .Include(p => p.PayrollRun)
            .FirstOrDefaultAsync(
                p => p.Year == year && p.Month == month && !p.IsCancelled,
                cancellationToken);
    }

    public async Task<bool> HasActivePaymentForPeriodAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CnssContributionPayments.AnyAsync(
            p => p.Year == year && p.Month == month && !p.IsCancelled,
            cancellationToken);
    }

    public async Task<bool> HasActivePaymentForRunAsync(
        Guid payrollRunId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.CnssContributionPayments.AnyAsync(
            p => p.PayrollRunId == payrollRunId && !p.IsCancelled,
            cancellationToken);
    }

    public async Task AddAsync(CnssContributionPayment payment, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CnssContributionPayments.Add(payment);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CnssContributionPayment payment, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.CnssContributionPayments.Attach(payment);
        context.Entry(payment).State = EntityState.Modified;
        await context.SaveChangesAsync(cancellationToken);
    }
}
