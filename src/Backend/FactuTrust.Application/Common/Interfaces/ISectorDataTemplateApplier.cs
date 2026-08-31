using FactuTrust.Application.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Applies the sector data templates matching a tenant's segment/domain classification to that
/// tenant's own database (plan §WP-B6, D5). Additive-only: every item handler checks-before-insert
/// and never updates or deletes an existing tenant row (mirrors
/// <c>WithholdingChartAccountsInitializer</c>/<c>TenantRuntimeCatalogBootstrapper</c>'s idempotency
/// idiom). Matching templates come from <c>ISectorCatalogProvider.GetSnapshot().DataTemplates</c> —
/// the static provider always returns an empty list, so this is a no-op for free whenever
/// <c>UseDbRules</c> is off (plan §WP-B2).
/// </summary>
/// <remarks>
/// Deviates from the plan's literal <c>ApplyAsync(tenantId, segmentCode, domainCode, dryRun, ct)</c>
/// signature by requiring the tenant's <paramref name="connectionString"/> explicitly. Reason: at
/// provisioning time (<c>TenantService.CreateTenantDatabaseAsync</c>) the connection string is not
/// yet persisted in the master DB when this must run, so resolving it internally via
/// <c>ITenantService.GetConnectionStringAsync(tenantId)</c> would fail for a brand-new tenant.
/// Callers that only have a <c>tenantId</c> (e.g. the future WP-B7 reconfiguration flow) resolve the
/// connection string themselves first.
/// </remarks>
public interface ISectorDataTemplateApplier
{
    /// <param name="tenantId">Used only for structured logging (no PII) — never for DB lookups here.</param>
    /// <param name="connectionString">The tenant database's own connection string.</param>
    /// <param name="segmentCode">Tenant's resolved <c>CompanySegment</c> (normalized code), or null.</param>
    /// <param name="domainCode">Tenant's resolved <c>BusinessDomain</c> (normalized code), or null.</param>
    /// <param name="dryRun">
    /// When true, computes <see cref="SectorTemplateApplyResult.ItemOutcomes"/> without writing
    /// anything (no <c>SaveChanges</c>) — used by WP-B7's preview endpoint.
    /// </param>
    Task<SectorTemplateApplyResult> ApplyAsync(
        Guid tenantId,
        string connectionString,
        string? segmentCode,
        string? domainCode,
        bool dryRun,
        CancellationToken cancellationToken = default);
}
