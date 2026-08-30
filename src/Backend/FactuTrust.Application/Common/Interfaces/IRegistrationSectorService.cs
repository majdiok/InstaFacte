using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Sector-aware registration wizard seam (plan §3 C8, §6.1 B3/B4). Phase 1 implementation
/// (<c>RegistrationSectorService</c>) resolves against the static
/// <see cref="SectorConfigurationCatalog"/>; Phase 2 swaps the internal resolution to a DB-backed
/// rule engine behind this same interface — zero controller changes at swap time.
/// </summary>
public interface IRegistrationSectorService
{
    /// <summary>
    /// Validates + resolves the sector profile for a registration payload.
    /// <list type="bullet">
    ///   <item>Kill-switch off, or both codes null/blank ⇒ success with a null value (exact legacy behavior).</item>
    ///   <item>Provided-but-unknown segment/domain, or domain without segment ⇒ failure with a French validation message.</item>
    ///   <item>Otherwise ⇒ success with the resolved <see cref="SectorProfile"/>.</item>
    /// </list>
    /// </summary>
    Result<SectorProfile?> ResolveProfile(string? companySegment, string? businessDomain);

    /// <summary>
    /// Computes the restriction-only module set for the given profile/request and writes the
    /// corresponding <c>UserModuleGrant</c> rows for <paramref name="userId"/>. Does NOT call
    /// <c>SaveChangesAsync</c> — the rows must ride the caller's existing transaction/SaveChanges.
    /// <paramref name="requestedModules"/> null/empty ⇒ no-op (writes nothing ⇒ legacy all-modules
    /// behavior, since <c>UserModuleGrant</c> absence means "all modules enabled").
    /// </summary>
    Task ApplyModuleSelectionAsync(
        Guid userId,
        SectorProfile? profile,
        IReadOnlyList<int>? requestedModules,
        SubscriptionPlan plan,
        CancellationToken cancellationToken);
}
