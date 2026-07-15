using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Services.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for payroll year parameters, with default seeding on first access.
/// </summary>
public sealed class PayrollParametersRepository : IPayrollParametersRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public PayrollParametersRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<PayrollYearParameters?> GetByFiscalYearAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollYearParameters
            .Include(p => p.IrppBrackets)
            .FirstOrDefaultAsync(p => p.FiscalYear == fiscalYear, cancellationToken);
    }

    public async Task<PayrollYearParameters> GetOrCreateForYearAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        var existing = await context.PayrollYearParameters
            .Include(p => p.IrppBrackets)
            .FirstOrDefaultAsync(p => p.FiscalYear == fiscalYear, cancellationToken);
        if (existing is not null)
            return existing;

        var defaultsResult = PayrollParameterDefaults.CreateDefaults(fiscalYear);
        if (defaultsResult.IsFailure)
            throw new InvalidOperationException($"Impossible de créer les paramètres de paie par défaut : {defaultsResult.Error.Description}");

        var created = defaultsResult.Value;
        context.PayrollYearParameters.Add(created);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Concurrent seeding (unique index on FiscalYear) — reload the row created by the other request.
            await using var retryContext = _contextFactory.CreateContext();
            var concurrent = await retryContext.PayrollYearParameters
                .Include(p => p.IrppBrackets)
                .FirstOrDefaultAsync(p => p.FiscalYear == fiscalYear, cancellationToken);
            if (concurrent is not null)
                return concurrent;
            throw;
        }

        return created;
    }

    public async Task<IReadOnlyList<PayrollYearParameters>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await context.PayrollYearParameters
            .Include(p => p.IrppBrackets)
            .OrderByDescending(p => p.FiscalYear)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(PayrollYearParameters parameters, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        // Replace brackets so removed ones are deleted rather than orphaned.
        await context.PayrollIrppBrackets
            .Where(b => b.PayrollYearParametersId == parameters.Id)
            .ExecuteDeleteAsync(cancellationToken);

        var tracked = await context.PayrollYearParameters
            .FirstOrDefaultAsync(p => p.Id == parameters.Id, cancellationToken);

        if (tracked is null)
        {
            context.PayrollYearParameters.Add(parameters);
        }
        else
        {
            context.Entry(tracked).CurrentValues.SetValues(parameters);
            foreach (var bracket in parameters.IrppBrackets)
                context.PayrollIrppBrackets.Add(bracket);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
