using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Services.Payroll;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
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
            .Include(p => p.GarnishmentBrackets)
            .FirstOrDefaultAsync(p => p.FiscalYear == fiscalYear, cancellationToken);
    }

    public async Task<PayrollYearParameters> GetOrCreateForYearAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        var existing = await context.PayrollYearParameters
            .Include(p => p.IrppBrackets)
            .Include(p => p.GarnishmentBrackets)
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
                .Include(p => p.GarnishmentBrackets)
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
            .Include(p => p.GarnishmentBrackets)
            .OrderByDescending(p => p.FiscalYear)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(PayrollYearParameters parameters, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();

        // Replace brackets so removed ones are deleted rather than orphaned.
        var bracketsToDelete = await context.PayrollIrppBrackets
            .Where(b => b.PayrollYearParametersId == parameters.Id)
            .ToListAsync(cancellationToken);
        context.PayrollIrppBrackets.RemoveRange(bracketsToDelete);

        var garnishmentToDelete = await context.PayrollGarnishmentBrackets
            .Where(b => b.PayrollYearParametersId == parameters.Id)
            .ToListAsync(cancellationToken);
        context.PayrollGarnishmentBrackets.RemoveRange(garnishmentToDelete);

        var tracked = await context.PayrollYearParameters
            .FirstOrDefaultAsync(p => p.Id == parameters.Id, cancellationToken);

        if (tracked is null)
        {
            context.PayrollYearParameters.Add(parameters);
            foreach (var bracket in parameters.IrppBrackets)
                AddIrppBracket(context, bracket, parameters.Id);
            foreach (var bracket in parameters.GarnishmentBrackets)
                AddGarnishmentBracket(context, bracket, parameters.Id);
        }
        else
        {
            context.Entry(tracked).CurrentValues.SetValues(parameters);
            foreach (var bracket in parameters.IrppBrackets)
                AddIrppBracket(context, bracket, tracked.Id);
            foreach (var bracket in parameters.GarnishmentBrackets)
                AddGarnishmentBracket(context, bracket, tracked.Id);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static void AddIrppBracket(TenantDbContext context, PayrollIrppBracket bracket, Guid parentId)
    {
        var entry = context.PayrollIrppBrackets.Add(bracket);
        if (bracket.PayrollYearParametersId == Guid.Empty)
            entry.Property(b => b.PayrollYearParametersId).CurrentValue = parentId;
    }

    private static void AddGarnishmentBracket(TenantDbContext context, PayrollGarnishmentBracket bracket, Guid parentId)
    {
        var entry = context.PayrollGarnishmentBrackets.Add(bracket);
        if (bracket.PayrollYearParametersId == Guid.Empty)
            entry.Property(b => b.PayrollYearParametersId).CurrentValue = parentId;
    }
}
