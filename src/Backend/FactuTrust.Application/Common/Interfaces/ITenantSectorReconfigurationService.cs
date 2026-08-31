using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Phase 2 — tenant sector re-configuration: preview and apply a segment/domain change for an
/// existing tenant from the backoffice (plan §WP-B7, D6). Module grants are recomputed via the
/// shared <c>SectorModuleSetCalculator</c>; templates are applied additively; every step is
/// fault-isolated (fail-continue) and audit-logged in the tenant DB.
/// </summary>
public interface ITenantSectorReconfigurationService
{
    /// <summary>
    /// Error code used for the tenant-not-found case (mapped to HTTP 404 by the controller). Any
    /// other validation error code (unknown/unlinked segment/domain) is mapped to HTTP 400.
    /// </summary>
    const string TenantNotFoundCode = "Validation.TenantNotFound";

    /// <summary>
    /// Computes a dry-run preview of the requested re-configuration. Strictly no side effects —
    /// templates are evaluated with <c>dryRun=true</c> and no <c>SaveChangesAsync</c> is issued.
    /// Returns a failure (<see cref="Result.IsFailure"/>) with the French validation message for an
    /// unknown/unlinked segment or domain; the caller maps that to a 400.
    /// </summary>
    Task<Result<SectorReconfigurationPreviewDto>> PreviewAsync(
        Guid tenantId,
        SectorReconfigurationRequestDto request,
        Guid? actorAdminId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies the requested re-configuration with per-step fault isolation (a failed step is
    /// recorded and the run continues). Returns the step outcomes plus a preview of the effective
    /// change. Validation failures (unknown/unlinked segment/domain) short-circuit before any step
    /// runs and are returned as a failure.
    /// </summary>
    Task<Result<SectorReconfigurationApplyResultDto>> ApplyAsync(
        Guid tenantId,
        SectorReconfigurationRequestDto request,
        Guid? actorAdminId,
        CancellationToken cancellationToken);
}
