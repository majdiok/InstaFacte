using FactuTrust.Infrastructure.Accounting;
using FactuTrust.Infrastructure.Persistence;

namespace FactuTrust.Infrastructure.MultiTenancy;

/// <summary>
/// Idempotent NCT + withholding catalog bootstrap shared by template maintenance
/// and <see cref="TenantMigrationGuard"/>.
/// </summary>
public static class TenantRuntimeCatalogBootstrapper
{
    public static async Task EnsureAsync(TenantDbContext context, CancellationToken cancellationToken = default)
    {
        await Nct01ChartMigrationService.EnsureMigratedAsync(context, cancellationToken);
        await WithholdingTaxCatalogInitializer.EnsureSystemTypesSeededAsync(context, cancellationToken);
        await WithholdingFiscalYearParameterInitializer.EnsureDefaultsSeededAsync(context, cancellationToken);
        await IncomeTaxYearParameterInitializer.EnsureDefaultsSeededAsync(context, cancellationToken);
        await WithholdingChartAccountsInitializer.EnsureAccountsAsync(context, cancellationToken);
    }
}
