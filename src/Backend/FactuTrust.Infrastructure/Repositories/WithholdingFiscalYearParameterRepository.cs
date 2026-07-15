using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class WithholdingFiscalYearParameterRepository : IWithholdingFiscalYearParameterRepository
{
    private readonly TenantDbContextFactory _contextFactory;

    public WithholdingFiscalYearParameterRepository(TenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<decimal> GetRs7TtcThresholdAsync(int fiscalYear, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateContext();
        var row = await context.WithholdingFiscalYearParameters
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.FiscalYear == fiscalYear, ct);

        return row?.Rs7TtcThresholdTnd ?? WithholdingTaxCalculationService.Rs7TtcThresholdTnd;
    }
}
