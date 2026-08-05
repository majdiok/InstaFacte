using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class PayrollPaymentRepository : IPayrollPaymentRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public PayrollPaymentRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PayrollPayment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollPayments.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<PayrollPayment?> GetByIdWithLinesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollPayments
            .Include(p => p.Lines)
            .Include(p => p.PayrollRun)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollPayment>> ListByPayrollRunAsync(
        Guid runId,
        bool includeCancelled,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        var query = context.PayrollPayments
            .AsNoTracking()
            .Include(p => p.Lines)
            .Where(p => p.PayrollRunId == runId);

        if (!includeCancelled)
            query = query.Where(p => !p.IsCancelled);

        return await query
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollPaymentLine>> ListLinesByPayslipAsync(
        Guid payslipId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollPaymentLines
            .AsNoTracking()
            .Where(l => l.PayslipId == payslipId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PayrollPayment payment, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PayrollPayments.Add(payment);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PayrollPayment payment, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        context.PayrollPayments.Attach(payment);
        context.Entry(payment).State = EntityState.Modified;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePayslipsAsync(IEnumerable<Payslip> payslips, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        foreach (var payslip in payslips)
        {
            context.Payslips.Attach(payslip);
            context.Entry(payslip).State = EntityState.Modified;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
