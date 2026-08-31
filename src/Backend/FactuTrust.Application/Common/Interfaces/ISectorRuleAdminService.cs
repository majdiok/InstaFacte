using FactuTrust.Application.Common;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Phase 2 — moteur de règles sectorielles en base : backoffice admin CRUD (plan §WP-B5).
/// Every successful write bumps <c>SectorRuleSetStamps.Version</c> inside the same
/// <c>SaveChangesAsync</c> — this is the cache-busting stamp <c>DbSectorCatalogProvider</c> keys on.
/// Deletes are always soft (<c>IsActive=false</c>); nothing is ever hard-deleted.
/// </summary>
public interface ISectorRuleAdminService
{
    Task<SectorRuleSetAdminDto> GetFullSetAsync(CancellationToken cancellationToken);
    Task<long> GetVersionAsync(CancellationToken cancellationToken);
    Task<SectorRuleSeedResult> SeedFromCatalogAsync(bool force, string? actor, CancellationToken cancellationToken);

    Task<IReadOnlyList<SectorSegmentAdminDto>> ListSegmentsAsync(CancellationToken cancellationToken);
    Task<Result<SectorSegmentAdminDto>> GetSegmentAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<SectorSegmentAdminDto>> CreateSegmentAsync(CreateSectorSegmentRequest request, string? actor, CancellationToken cancellationToken);
    Task<Result<SectorSegmentAdminDto>> UpdateSegmentAsync(Guid id, UpdateSectorSegmentRequest request, string? actor, CancellationToken cancellationToken);
    Task<Result<bool>> DeactivateSegmentAsync(Guid id, string? actor, CancellationToken cancellationToken);

    Task<IReadOnlyList<SectorDomainAdminDto>> ListDomainsAsync(CancellationToken cancellationToken);
    Task<Result<SectorDomainAdminDto>> GetDomainAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<SectorDomainAdminDto>> CreateDomainAsync(CreateSectorDomainRequest request, string? actor, CancellationToken cancellationToken);
    Task<Result<SectorDomainAdminDto>> UpdateDomainAsync(Guid id, UpdateSectorDomainRequest request, string? actor, CancellationToken cancellationToken);
    Task<Result<bool>> DeactivateDomainAsync(Guid id, string? actor, CancellationToken cancellationToken);

    Task<IReadOnlyList<SectorSegmentDomainAdminDto>> ListSegmentDomainsAsync(CancellationToken cancellationToken);
    Task<Result<SectorSegmentDomainAdminDto>> GetSegmentDomainAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<SectorSegmentDomainAdminDto>> CreateSegmentDomainAsync(CreateSectorSegmentDomainRequest request, string? actor, CancellationToken cancellationToken);
    Task<Result<SectorSegmentDomainAdminDto>> UpdateSegmentDomainAsync(Guid id, UpdateSectorSegmentDomainRequest request, string? actor, CancellationToken cancellationToken);
    Task<Result<bool>> DeactivateSegmentDomainAsync(Guid id, string? actor, CancellationToken cancellationToken);

    Task<IReadOnlyList<SectorModuleRuleAdminDto>> ListModuleRulesAsync(CancellationToken cancellationToken);
    Task<Result<SectorModuleRuleAdminDto>> GetModuleRuleAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<SectorModuleRuleAdminDto>> CreateModuleRuleAsync(CreateSectorModuleRuleRequest request, string? actor, CancellationToken cancellationToken);
    Task<Result<SectorModuleRuleAdminDto>> UpdateModuleRuleAsync(Guid id, UpdateSectorModuleRuleRequest request, string? actor, CancellationToken cancellationToken);
    Task<Result<bool>> DeactivateModuleRuleAsync(Guid id, string? actor, CancellationToken cancellationToken);

    Task<IReadOnlyList<SectorModuleDependencyAdminDto>> ListModuleDependenciesAsync(CancellationToken cancellationToken);
    Task<Result<SectorModuleDependencyAdminDto>> GetModuleDependencyAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<SectorModuleDependencyAdminDto>> CreateModuleDependencyAsync(CreateSectorModuleDependencyRequest request, string? actor, CancellationToken cancellationToken);
    Task<Result<bool>> DeactivateModuleDependencyAsync(Guid id, string? actor, CancellationToken cancellationToken);

    Task<IReadOnlyList<SectorDefaultSettingAdminDto>> ListSettingsAsync(CancellationToken cancellationToken);
    Task<Result<SectorDefaultSettingAdminDto>> GetSettingAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<SectorDefaultSettingAdminDto>> CreateSettingAsync(CreateSectorDefaultSettingRequest request, string? actor, CancellationToken cancellationToken);
    Task<Result<SectorDefaultSettingAdminDto>> UpdateSettingAsync(Guid id, UpdateSectorDefaultSettingRequest request, string? actor, CancellationToken cancellationToken);
    Task<Result<bool>> DeactivateSettingAsync(Guid id, string? actor, CancellationToken cancellationToken);

    Task<IReadOnlyList<SectorDataTemplateAdminDto>> ListTemplatesAsync(CancellationToken cancellationToken);
    Task<Result<SectorDataTemplateAdminDto>> GetTemplateAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<SectorDataTemplateAdminDto>> CreateTemplateAsync(CreateSectorDataTemplateRequest request, string? actor, CancellationToken cancellationToken);
    Task<Result<SectorDataTemplateAdminDto>> UpdateTemplateAsync(Guid id, UpdateSectorDataTemplateRequest request, string? actor, CancellationToken cancellationToken);
    Task<Result<bool>> DeactivateTemplateAsync(Guid id, string? actor, CancellationToken cancellationToken);
}
