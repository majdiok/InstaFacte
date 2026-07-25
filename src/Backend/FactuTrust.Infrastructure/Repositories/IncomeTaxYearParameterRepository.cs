using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Fiscal;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class IncomeTaxYearParameterRepository : IIncomeTaxYearParameterRepository
{
    private readonly ITenantDbContextFactory _contextFactory;

    public IncomeTaxYearParameterRepository(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IncomeTaxYearParameter> GetOrDefaultAsync(int fiscalYear, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateContext();
        var row = await context.IncomeTaxYearParameters
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.FiscalYear == fiscalYear, ct);

        return row ?? IncomeTaxYearParameterDefaults.Create(fiscalYear);
    }

    public async Task<IncomeTaxYearParameter> UpsertAsync(int fiscalYear, IncomeTaxParameterUpsert u, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateContext();

        var row = await context.IncomeTaxYearParameters
            .FirstOrDefaultAsync(p => p.FiscalYear == fiscalYear, ct);

        if (row is null)
        {
            row = IncomeTaxYearParameterDefaults.Create(fiscalYear);
            context.IncomeTaxYearParameters.Add(row);
        }

        // markUserModified: true → la ligne devient intouchable par l'initialiseur de défauts.
        row.Update(
            u.IsStandardRate, u.IsReducedRate, u.IsSectorRate,
            u.MinTaxRate, u.MinTaxReducedRate, u.MinTaxFloorTnd,
            u.CssApplies, u.CssRate, u.CssFloorTnd,
            u.AcompteRate, u.AcompteCount, u.DeficitCarryForwardYears,
            u.IrppBracketsJson,
            u.MinTaxFloorReducedTnd, u.RoundTaxableToDinar,
            markUserModified: true);

        await context.SaveChangesAsync(ct);
        return row;
    }
}
