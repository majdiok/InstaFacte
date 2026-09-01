using FactuTrust.Application.Common;
using FactuTrust.Domain.Entities.SectorRules;
using FactuTrust.Domain.SectorConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FactuTrust.Infrastructure.Persistence.Seeds;

/// <summary>
/// Idempotent upsert-by-code seeder that projects the static <see cref="SectorConfigurationCatalog"/>
/// into the master-DB sector-rule tables (plan §WP-B3, D3). Mirrors <see cref="PlanSeeder"/>'s style.
///
/// Three modes (see <see cref="SeedMode"/>):
/// <list type="bullet">
/// <item><c>InsertMissing</c> (<see cref="SeedAsync"/> with <c>force=false</c>, or
/// <see cref="SeedIfEmptyAsync"/>): inserts rows missing from the DB only — never touches an
/// existing row, so admin edits made through the backoffice CRUD survive.</item>
/// <item><c>ForceReset</c> (<see cref="SeedAsync"/> with <c>force=true</c>, the admin
/// "restore factory defaults" endpoint): resets every catalog-known row back to the catalog's
/// values, reactivates it, and reclaims it as catalog-owned (<see cref="Entity"/> flag). Admin
/// rows the catalog never knows about (extra segments/domains/templates with non-catalog codes,
/// admin-added module rules) are never touched, and nothing is ever deleted.</item>
/// <item><c>Reconcile</c> (<see cref="ReconcileOnStartupAsync"/>): the startup path. Inserts
/// missing catalog rows, refreshes/reactivates catalog-owned rows, and deactivates catalog-owned
/// rows that were removed from the catalog — but NEVER overwrites or deactivates an
/// admin-managed row (those are logged as a parity divergence and left alone).</item>
/// </list>
///
/// Provenance: every sector-rule entity carries <c>IsManagedByCatalog</c>. The seeder only ever
/// mutates catalog-owned rows in <c>Reconcile</c> mode, while <c>ForceReset</c> claims back all
/// catalog-known rows. The backoffice CRUD marks rows admin-managed on create/update, so an
/// operator's customization is never silently clobbered by the next restart.
/// </summary>
public static class SectorRuleSeeder
{
    private const string GlobalPlanComptableVariantKey = "plan-comptable-variant";
    private const string DefaultWarehouseNameSettingKey = "default-warehouse-name";

    /// <summary>Actor recorded on rows written by an automatic (non-admin) seed run.</summary>
    private const string SeederActor = "catalog-seeder";

    /// <summary>Actor recorded on rows written by a startup reconciliation run.</summary>
    private const string StartupReconcileActor = "startup-reconcile";

    /// <summary>sp_getapplock resource used to serialize concurrent startup reconciliations.</summary>
    private const string ReconcileLockResource = "FactuTrust:SectorRuleReconcile";

    private enum SeedMode
    {
        InsertMissing,
        Reconcile,
        ForceReset
    }

    /// <summary>No-ops unless <c>SectorSegments</c> has zero rows — safe to call unconditionally at startup.</summary>
    public static async Task SeedIfEmptyAsync(MasterDbContext context, CancellationToken cancellationToken = default)
    {
        var hasAnySegment = await context.SectorSegments.AnyAsync(cancellationToken);
        if (hasAnySegment)
            return;

        await SeedAsync(context, force: false, actor: SeederActor, cancellationToken);
    }

    /// <summary>
    /// Deterministic SHA-256 hex digest of the in-memory catalog's full content (review R2) — used
    /// to detect a catalog change across app restarts without diffing every row. Built from the
    /// same <see cref="SectorConfigurationCatalog.BuildCatalogSnapshot"/> the parity checker and
    /// seeder itself consume, so any segment/domain/dependency/template addition, removal, or edit
    /// in the catalog source changes the hash.
    /// </summary>
    public static string ComputeCatalogHash()
    {
        var snapshot = SectorConfigurationCatalog.BuildCatalogSnapshot();
        var json = JsonSerializer.Serialize(snapshot);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Startup entry point (review R2 + review follow-up F1/F2/F3), replacing the old
    /// <see cref="SeedIfEmptyAsync"/> call in <c>Program.cs</c>. An empty <c>SectorSegments</c>
    /// table still gets a plain seed; an EXISTING deployment reconciles automatically whenever the
    /// catalog content hash has drifted from the recorded stamp.
    ///
    /// Unlike the first revision (which reused a destructive <c>force=true</c> seed), this path is
    /// non-destructive toward admin customization: only catalog-owned rows are refreshed, and
    /// catalog-owned rows removed from the catalog are deactivated; admin-managed rows are left
    /// untouched and reported as parity divergences. The admin <c>seed-from-catalog?force=true</c>
    /// endpoint remains the explicit factory-reset.
    ///
    /// Concurrency: multiple app instances can start simultaneously. On a relational provider the
    /// reconciliation is serialized with a transaction-scoped <c>sp_getapplock</c>; the losing
    /// instance re-checks the hash after the lock clears and no-ops if it has since converged. On a
    /// non-relational test host (InMemory) the lock is a no-op and the idempotent single-commit
    /// seed is sufficient.
    /// </summary>
    public static async Task<SectorRuleSeedResult> ReconcileOnStartupAsync(
        MasterDbContext context,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsRelational())
        {
            // InMemory test host: no transaction/app-lock support. The hash-check + idempotent
            // single-commit seed below is still race-free enough for a single-threaded test.
            return await ReconcileCoreAsync(context, logger, cancellationToken);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        if (!await TryAcquireAppLockAsync(context, ReconcileLockResource, cancellationToken))
        {
            // Another instance held the lock and has likely just reconciled. Roll back our empty
            // transaction and re-check the hash against whatever the winner left behind.
            await transaction.RollbackAsync(cancellationToken);
            return await NoopIfHashConvergedAsync(context, logger, cancellationToken);
        }

        try
        {
            var result = await ReconcileCoreAsync(context, logger, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<SectorRuleSeedResult> ReconcileCoreAsync(
        MasterDbContext context,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var hasAnySegment = await context.SectorSegments.AnyAsync(cancellationToken);
        if (!hasAnySegment)
            return await SeedAsync(context, force: false, actor: SeederActor, cancellationToken);

        var currentHash = ComputeCatalogHash();
        var stamp = await context.SectorRuleSetStamps.SingleOrDefaultAsync(
            s => s.Id == SectorRuleSetStamp.SingletonId, cancellationToken);

        if (stamp is not null && string.Equals(stamp.CatalogContentHash, currentHash, StringComparison.Ordinal))
            return new SectorRuleSeedResult(0, 0, 0, stamp.Version, Forced: false);

        return await SeedInternalAsync(
            context, SeedMode.Reconcile, StartupReconcileActor, logger, cancellationToken);
    }

    private static async Task<SectorRuleSeedResult> NoopIfHashConvergedAsync(
        MasterDbContext context, ILogger? logger, CancellationToken cancellationToken)
    {
        var stamp = await context.SectorRuleSetStamps.SingleOrDefaultAsync(
            s => s.Id == SectorRuleSetStamp.SingletonId, cancellationToken);
        if (stamp is not null && string.Equals(stamp.CatalogContentHash, ComputeCatalogHash(), StringComparison.Ordinal))
            return new SectorRuleSeedResult(0, 0, 0, stamp.Version, Forced: false);

        // The winner failed to converge (or is still running) — fall through to a normal reconcile.
        return await ReconcileCoreAsync(context, logger, cancellationToken);
    }

    /// <summary>
    /// Acquires a transaction-scoped SQL Server application lock, returning <c>false</c> only when
    /// another instance already holds it (the lock-timeout path). A <c>false</c> result means "go
    /// re-check the hash" rather than "run the seed anyway".
    /// </summary>
    private static async Task<bool> TryAcquireAppLockAsync(
        MasterDbContext context, string resource, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = "sp_getapplock";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 40;

            void AddParameter(string name, object value)
            {
                var p = command.CreateParameter();
                p.ParameterName = name;
                p.Value = value;
                command.Parameters.Add(p);
            }

            AddParameter("@Resource", resource);
            AddParameter("@LockMode", "Exclusive");
            AddParameter("@LockOwner", "Transaction");
            AddParameter("@LockTimeout", 30000);
            AddParameter("@DbPrincipal", "public");

            var returnParameter = command.CreateParameter();
            returnParameter.ParameterName = "@ReturnValue";
            returnParameter.Direction = ParameterDirection.ReturnValue;
            command.Parameters.Add(returnParameter);

            await command.ExecuteNonQueryAsync(cancellationToken);
            var status = (int)(returnParameter.Value ?? -999);
            // 0 = granted immediately, 1 = granted after waiting. Anything else = not acquired.
            return status is 0 or 1;
        }
        finally
        {
            if (wasClosed)
                await connection.CloseAsync();
        }
    }

    public static Task<SectorRuleSeedResult> SeedAsync(
        MasterDbContext context,
        bool force,
        string? actor,
        CancellationToken cancellationToken = default)
        => SeedInternalAsync(
            context, force ? SeedMode.ForceReset : SeedMode.InsertMissing, actor ?? SeederActor, null, cancellationToken);

    private static async Task<SectorRuleSeedResult> SeedInternalAsync(
        MasterDbContext context,
        SeedMode mode,
        string? actor,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var effectiveActor = actor ?? SeederActor;
        var inserted = 0;
        var updated = 0;
        var skippedExisting = 0;
        var divergences = new List<string>();

        // Only Reconcile mode collects (and later logs) admin-parity divergences.
        void RecordDivergence(string message)
        {
            if (mode == SeedMode.Reconcile)
                divergences.Add(message);
        }

        // ---------- Segments ----------
        var segmentsByCode = (await context.SectorSegments.ToListAsync(cancellationToken))
            .ToDictionary(s => s.Code, StringComparer.Ordinal);
        var catalogSegmentCodes = SectorConfigurationCatalog.Segments.Select(s => s.Code).ToHashSet(StringComparer.Ordinal);

        foreach (var def in SectorConfigurationCatalog.Segments)
        {
            if (segmentsByCode.TryGetValue(def.Code, out var existing))
            {
                if (mode == SeedMode.InsertMissing)
                {
                    skippedExisting++;
                }
                else if (mode == SeedMode.Reconcile && !existing.IsManagedByCatalog)
                {
                    RecordDivergence($"Segment '{def.Code}' left unchanged (admin-managed).");
                    skippedExisting++;
                }
                else
                {
                    existing.ResetFromCatalog(def.LabelFr, def.DescriptionFr, def.IconKey, def.SortOrder, def.DefaultWarehouseName);
                    existing.MarkCatalogManaged();
                    existing.SetAuditInfo(effectiveActor, isUpdate: true);
                    updated++;
                }
            }
            else
            {
                var created = SectorSegment.Create(def.Code, def.LabelFr, def.DescriptionFr, def.IconKey, def.SortOrder, def.DefaultWarehouseName);
                created.SetAuditInfo(effectiveActor);
                context.SectorSegments.Add(created);
                segmentsByCode[def.Code] = created;
                inserted++;
            }
        }

        // Deactivate catalog-owned segments that were removed from the catalog (review F3).
        foreach (var (code, segment) in segmentsByCode)
        {
            if (catalogSegmentCodes.Contains(code) || !segment.IsActive)
                continue;
            if (!segment.IsManagedByCatalog)
            {
                RecordDivergence($"Removed-catalog segment '{code}' left active (admin-managed).");
                continue;
            }
            segment.Deactivate();
            segment.SetAuditInfo(effectiveActor, isUpdate: true);
            updated++;
        }

        // ---------- Domains ----------
        var domainsByCode = (await context.SectorDomains.ToListAsync(cancellationToken))
            .ToDictionary(d => d.Code, StringComparer.Ordinal);
        var catalogDomainCodes = SectorConfigurationCatalog.Domains.Select(d => d.Code).ToHashSet(StringComparer.Ordinal);

        foreach (var def in SectorConfigurationCatalog.Domains)
        {
            if (domainsByCode.TryGetValue(def.Code, out var existing))
            {
                if (mode == SeedMode.InsertMissing)
                {
                    skippedExisting++;
                }
                else if (mode == SeedMode.Reconcile && !existing.IsManagedByCatalog)
                {
                    RecordDivergence($"Domaine '{def.Code}' left unchanged (admin-managed).");
                    skippedExisting++;
                }
                else
                {
                    existing.ResetFromCatalog(def.LabelFr, def.SortOrder);
                    existing.MarkCatalogManaged();
                    existing.SetAuditInfo(effectiveActor, isUpdate: true);
                    updated++;
                }
            }
            else
            {
                var created = SectorDomain.Create(def.Code, def.LabelFr, def.SortOrder);
                created.SetAuditInfo(effectiveActor);
                context.SectorDomains.Add(created);
                domainsByCode[def.Code] = created;
                inserted++;
            }
        }

        foreach (var (code, domain) in domainsByCode)
        {
            if (catalogDomainCodes.Contains(code) || !domain.IsActive)
                continue;
            if (!domain.IsManagedByCatalog)
            {
                RecordDivergence($"Removed-catalog domaine '{code}' left active (admin-managed).");
                continue;
            }
            domain.Deactivate();
            domain.SetAuditInfo(effectiveActor, isUpdate: true);
            updated++;
        }

        // A brand-new segment/domain has a client-generated Guid (Entity ctor), so no intermediate
        // SaveChangesAsync is needed before wiring the links/rules below — parent-before-child Add
        // order + the single save at the end keeps FK ordering safe (see MasterDbContext: no
        // navigations, so EF cannot reorder inserts topologically).

        // ---------- Segment ↔ Domain links (plan §3.1/§3.2 matrix) ----------
        var existingLinks = (await context.SectorSegmentDomains.ToListAsync(cancellationToken))
            .ToDictionary(l => (l.SegmentId, l.DomainId));

        foreach (var segmentDef in SectorConfigurationCatalog.Segments)
        {
            var segment = segmentsByCode[segmentDef.Code];
            var allowedDomainCodes = new HashSet<string>(segmentDef.AllowedDomainCodes, StringComparer.Ordinal);

            foreach (var domainDef in SectorConfigurationCatalog.Domains)
            {
                var domain = domainsByCode[domainDef.Code];
                var key = (segment.Id, domain.Id);
                var isMatrixPair = allowedDomainCodes.Contains(domainDef.Code);

                if (existingLinks.TryGetValue(key, out var existingLink))
                {
                    if (isMatrixPair)
                    {
                        if (mode == SeedMode.InsertMissing)
                        {
                            skippedExisting++;
                        }
                        else if (mode == SeedMode.Reconcile && !existingLink.IsManagedByCatalog)
                        {
                            RecordDivergence($"Link {segmentDef.Code}→{domainDef.Code} left active (admin-managed, may be pruned).");
                            skippedExisting++;
                        }
                        else
                        {
                            existingLink.UpdateSortOrder(domainDef.SortOrder);
                            existingLink.Reactivate();
                            existingLink.MarkCatalogManaged();
                            existingLink.SetAuditInfo(effectiveActor, isUpdate: true);
                            updated++;
                        }
                    }
                    else
                    {
                        // Catalog-known pair no longer in the matrix — deactivate (never delete).
                        if (!existingLink.IsActive)
                        {
                            skippedExisting++;
                        }
                        else if (mode == SeedMode.Reconcile && !existingLink.IsManagedByCatalog)
                        {
                            RecordDivergence($"Non-matrix link {segmentDef.Code}→{domainDef.Code} left active (admin-managed).");
                            skippedExisting++;
                        }
                        else
                        {
                            existingLink.Deactivate();
                            existingLink.SetAuditInfo(effectiveActor, isUpdate: true);
                            updated++;
                        }
                    }
                }
                else if (isMatrixPair)
                {
                    var created = SectorSegmentDomain.Create(segment.Id, domain.Id, domainDef.SortOrder);
                    created.SetAuditInfo(effectiveActor);
                    context.SectorSegmentDomains.Add(created);
                    existingLinks[key] = created;
                    inserted++;
                }
            }
        }

        // ---------- Module rules: segment base recommendations + domain overlays ----------
        var existingSegmentBaseRules = (await context.SectorModuleRules
                .Where(r => r.RuleKind == SectorModuleRuleKind.SegmentBase)
                .ToListAsync(cancellationToken))
            .ToDictionary(r => (r.SegmentId, r.ModuleId));

        foreach (var segmentDef in SectorConfigurationCatalog.Segments)
        {
            var segment = segmentsByCode[segmentDef.Code];
            var recommended = new HashSet<int>(segmentDef.BaseRecommendedModules.Select(m => (int)m));
            var sortOrder = 0;
            foreach (var module in segmentDef.BaseRecommendedModules)
            {
                var key = ((Guid?)segment.Id, (int)module);
                if (existingSegmentBaseRules.TryGetValue(key, out var existingRule))
                {
                    if (mode == SeedMode.InsertMissing)
                    {
                        skippedExisting++;
                    }
                    else if (mode == SeedMode.Reconcile && !existingRule.IsManagedByCatalog)
                    {
                        RecordDivergence($"Base rule segment={segmentDef.Code} module={(int)module} left unchanged (admin-managed).");
                        skippedExisting++;
                    }
                    else
                    {
                        existingRule.UpdateSortOrder(sortOrder);
                        existingRule.Reactivate();
                        existingRule.MarkCatalogManaged();
                        existingRule.SetAuditInfo(effectiveActor, isUpdate: true);
                        updated++;
                    }
                }
                else
                {
                    var created = SectorModuleRule.CreateSegmentBase(segment.Id, (int)module, sortOrder);
                    created.SetAuditInfo(effectiveActor);
                    context.SectorModuleRules.Add(created);
                    existingSegmentBaseRules[key] = created;
                    inserted++;
                }

                sortOrder++;
            }

            // Deactivate catalog-owned base rules whose module was removed from this segment's
            // recommendation (review F3), but only when the segment is itself catalog-owned-active.
            var segmentActiveAndOwned = segment.IsActive && segment.IsManagedByCatalog;
            foreach (var ((segId, moduleId), rule) in existingSegmentBaseRules)
            {
                if (segId != segment.Id || recommended.Contains(moduleId) || !rule.IsActive)
                    continue;
                if (!segmentActiveAndOwned || !rule.IsManagedByCatalog)
                {
                    if (!rule.IsManagedByCatalog)
                        RecordDivergence($"Stale base rule segment={segmentDef.Code} module={moduleId} left active (admin-managed).");
                    continue;
                }
                rule.Deactivate();
                rule.SetAuditInfo(effectiveActor, isUpdate: true);
                updated++;
            }
        }

        // Remove-vs-rule cleanup for segments that were themselves deactivated above is unnecessary:
        // their rules are already inert once the segment is inactive (and were skipped here because
        // segmentActiveAndOwned is false). We leave the rows for auditability.

        var existingDomainOverlayRules = (await context.SectorModuleRules
                .Where(r => r.RuleKind == SectorModuleRuleKind.DomainOverlay)
                .ToListAsync(cancellationToken))
            .ToDictionary(r => (r.DomainId, r.ModuleId));

        foreach (var domainDef in SectorConfigurationCatalog.Domains)
        {
            var domain = domainsByCode[domainDef.Code];
            var recommended = new HashSet<int>(domainDef.OverlayModules.Select(m => (int)m));
            var sortOrder = 0;
            foreach (var module in domainDef.OverlayModules)
            {
                var key = ((Guid?)domain.Id, (int)module);
                if (existingDomainOverlayRules.TryGetValue(key, out var existingRule))
                {
                    if (mode == SeedMode.InsertMissing)
                    {
                        skippedExisting++;
                    }
                    else if (mode == SeedMode.Reconcile && !existingRule.IsManagedByCatalog)
                    {
                        RecordDivergence($"Overlay rule domaine={domainDef.Code} module={(int)module} left unchanged (admin-managed).");
                        skippedExisting++;
                    }
                    else
                    {
                        existingRule.UpdateSortOrder(sortOrder);
                        existingRule.Reactivate();
                        existingRule.MarkCatalogManaged();
                        existingRule.SetAuditInfo(effectiveActor, isUpdate: true);
                        updated++;
                    }
                }
                else
                {
                    var created = SectorModuleRule.CreateDomainOverlay(domain.Id, (int)module, sortOrder);
                    created.SetAuditInfo(effectiveActor);
                    context.SectorModuleRules.Add(created);
                    existingDomainOverlayRules[key] = created;
                    inserted++;
                }

                sortOrder++;
            }

            var domainActiveAndOwned = domain.IsActive && domain.IsManagedByCatalog;
            foreach (var ((domId, moduleId), rule) in existingDomainOverlayRules)
            {
                if (domId != domain.Id || recommended.Contains(moduleId) || !rule.IsActive)
                    continue;
                if (!domainActiveAndOwned || !rule.IsManagedByCatalog)
                {
                    if (!rule.IsManagedByCatalog)
                        RecordDivergence($"Stale overlay rule domaine={domainDef.Code} module={moduleId} left active (admin-managed).");
                    continue;
                }
                rule.Deactivate();
                rule.SetAuditInfo(effectiveActor, isUpdate: true);
                updated++;
            }
        }

        // ---------- Module dependencies (plan §4.2: catalog-declared edges only) ----------
        var existingDependencies = (await context.SectorModuleDependencies.ToListAsync(cancellationToken))
            .ToDictionary(d => (d.ModuleId, d.RequiredModuleId));

        var catalogDependencyKeys = new HashSet<(int ModuleId, int RequiredModuleId)>();
        foreach (var edge in SectorConfigurationCatalog.ModuleDependencies)
        {
            var key = (ModuleId: (int)edge.Module, RequiredModuleId: (int)edge.RequiredModule);
            catalogDependencyKeys.Add(key);

            if (existingDependencies.TryGetValue(key, out var existingDependency))
            {
                if (mode == SeedMode.InsertMissing)
                {
                    skippedExisting++;
                }
                else if (mode == SeedMode.Reconcile && !existingDependency.IsManagedByCatalog)
                {
                    RecordDivergence($"Dependency {(int)edge.Module}→{(int)edge.RequiredModule} left unchanged (admin-managed).");
                    skippedExisting++;
                }
                else
                {
                    existingDependency.Reactivate();
                    existingDependency.MarkCatalogManaged();
                    updated++;
                }
            }
            else
            {
                var created = SectorModuleDependency.Create(key.ModuleId, key.RequiredModuleId);
                context.SectorModuleDependencies.Add(created);
                existingDependencies[key] = created;
                inserted++;
            }
        }

        foreach (var (key, dependency) in existingDependencies)
        {
            if (catalogDependencyKeys.Contains(key) || !dependency.IsActive)
                continue;
            if (mode == SeedMode.Reconcile && !dependency.IsManagedByCatalog)
            {
                RecordDivergence($"Non-catalog dependency {key.ModuleId}→{key.RequiredModuleId} left active (admin-managed).");
                continue;
            }
            // ForceReset deactivates ANY non-catalog edge (bounded space, "restore defaults");
            // Reconcile deactivates only catalog-owned ones.
            dependency.Deactivate();
            updated++;
        }

        // ---------- Data templates + items (plan §4.3: additive sector presets) ----------
        var existingTemplatesByCode = (await context.SectorDataTemplates.ToListAsync(cancellationToken))
            .ToDictionary(t => t.Code, StringComparer.Ordinal);
        var catalogTemplateCodes = SectorConfigurationCatalog.DataTemplates.Select(t => t.Code).ToHashSet(StringComparer.Ordinal);

        foreach (var templateDef in SectorConfigurationCatalog.DataTemplates)
        {
            SectorDataTemplate template;
            if (existingTemplatesByCode.TryGetValue(templateDef.Code, out var existingTemplate))
            {
                template = existingTemplate;
                if (mode == SeedMode.InsertMissing)
                {
                    skippedExisting++;
                }
                else if (mode == SeedMode.Reconcile && !existingTemplate.IsManagedByCatalog)
                {
                    RecordDivergence($"Template '{templateDef.Code}' left unchanged (admin-managed).");
                    skippedExisting++;
                }
                else
                {
                    existingTemplate.UpdateDetails(templateDef.LabelFr, templateDef.DescriptionFr, templateDef.Version, templateDef.SortOrder);
                    existingTemplate.UpdateScope(templateDef.SegmentCode, templateDef.DomainCode);
                    existingTemplate.Reactivate();
                    existingTemplate.MarkCatalogManaged();
                    updated++;
                }
            }
            else
            {
                template = SectorDataTemplate.Create(
                    templateDef.Code,
                    templateDef.SegmentCode,
                    templateDef.DomainCode,
                    templateDef.LabelFr,
                    templateDef.DescriptionFr,
                    templateDef.Version,
                    templateDef.SortOrder);
                context.SectorDataTemplates.Add(template);
                existingTemplatesByCode[templateDef.Code] = template;
                inserted++;
            }

            // Items are matched by natural key (ItemKind, SortOrder) within their template. Build
            // the dictionary defensively (first-wins) so a legacy duplicate never crashes the seed.
            var existingItemsByKey = new Dictionary<(string ItemKind, int SortOrder), SectorDataTemplateItem>();
            var templateItems = await context.SectorDataTemplateItems
                .Where(i => i.TemplateId == template.Id)
                .ToListAsync(cancellationToken);
            foreach (var item in templateItems)
            {
                var itemKey = (item.ItemKind, item.SortOrder);
                existingItemsByKey.TryAdd(itemKey, item);
            }

            var catalogItemKeys = new HashSet<(string ItemKind, int SortOrder)>();
            foreach (var itemDef in templateDef.Items)
            {
                var itemKey = (itemDef.ItemKind, itemDef.SortOrder);
                catalogItemKeys.Add(itemKey);

                if (existingItemsByKey.TryGetValue(itemKey, out var existingItem))
                {
                    if (mode == SeedMode.InsertMissing)
                    {
                        skippedExisting++;
                    }
                    else if (mode == SeedMode.Reconcile && !existingItem.IsManagedByCatalog)
                    {
                        RecordDivergence($"Template item '{templateDef.Code}'/{itemDef.ItemKind}#{itemDef.SortOrder} left unchanged (admin-managed).");
                        skippedExisting++;
                    }
                    else
                    {
                        existingItem.UpdatePayload(itemDef.PayloadJson);
                        if (!existingItem.IsActive)
                            existingItem.Reactivate();
                        existingItem.MarkCatalogManaged();
                        updated++;
                    }
                }
                else
                {
                    var createdItem = SectorDataTemplateItem.Create(template.Id, itemDef.ItemKind, itemDef.PayloadJson, itemDef.SortOrder);
                    context.SectorDataTemplateItems.Add(createdItem);
                    existingItemsByKey[itemKey] = createdItem;
                    inserted++;
                }
            }

            // Deactivate catalog-owned items removed from the catalog (review F3).
            var templateActiveAndOwned = template.IsActive && template.IsManagedByCatalog;
            foreach (var (itemKey, item) in existingItemsByKey)
            {
                if (catalogItemKeys.Contains(itemKey) || !item.IsActive)
                    continue;
                if (!templateActiveAndOwned || !item.IsManagedByCatalog)
                {
                    if (!item.IsManagedByCatalog)
                        RecordDivergence($"Stale template item '{templateDef.Code}'/{itemKey.ItemKind}#{itemKey.SortOrder} left active (admin-managed).");
                    continue;
                }
                item.Deactivate();
                updated++;
            }
        }

        // Deactivate catalog-owned templates removed from the catalog entirely (review F3).
        foreach (var (code, template) in existingTemplatesByCode)
        {
            if (catalogTemplateCodes.Contains(code) || !template.IsActive)
                continue;
            if (!template.IsManagedByCatalog)
            {
                RecordDivergence($"Removed-catalog template '{code}' left active (admin-managed).");
                continue;
            }
            template.Deactivate();
            template.SetAuditInfo(effectiveActor, isUpdate: true);
            updated++;
        }

        // ---------- Default settings: per-segment default warehouse name + one global row ----------
        var existingSettings = (await context.SectorDefaultSettings.ToListAsync(cancellationToken))
            .ToDictionary(s => (s.SegmentCode, s.DomainCode, s.SettingKey), StringTupleComparer.Instance);

        var settingSortOrder = 0;
        var catalogSettingKeys = new HashSet<(string? SegmentCode, string? DomainCode, string SettingKey)>();
        foreach (var segmentDef in SectorConfigurationCatalog.Segments)
        {
            if (segmentDef.DefaultWarehouseName is null)
                continue;

            var key = (segmentDef.Code, (string?)null, DefaultWarehouseNameSettingKey);
            catalogSettingKeys.Add(key);
            UpsertDefaultSetting(
                context, existingSettings, segmentDef.Code, domainCode: null,
                DefaultWarehouseNameSettingKey, segmentDef.DefaultWarehouseName, "string",
                settingSortOrder, mode, effectiveActor, RecordDivergence,
                ref inserted, ref updated, ref skippedExisting);

            settingSortOrder++;
        }

        var globalKey = ((string?)null, (string?)null, GlobalPlanComptableVariantKey);
        catalogSettingKeys.Add(globalKey);
        UpsertDefaultSetting(
            context, existingSettings, segmentCode: null, domainCode: null,
            GlobalPlanComptableVariantKey, "nct01", "string",
            settingSortOrder, mode, effectiveActor, RecordDivergence,
            ref inserted, ref updated, ref skippedExisting);

        // Deactivate catalog-owned settings removed from the catalog (review F3).
        foreach (var (key, setting) in existingSettings)
        {
            if (catalogSettingKeys.Contains(key) || !setting.IsActive)
                continue;
            if (!setting.IsManagedByCatalog)
            {
                RecordDivergence($"Removed-catalog setting '{key.SettingKey}' ({key.SegmentCode}/{key.DomainCode}) left active (admin-managed).");
                continue;
            }
            setting.Deactivate();
            setting.SetAuditInfo(effectiveActor, isUpdate: true);
            updated++;
        }

        // ---------- Version stamp: bumped exactly once per run, in the same SaveChangesAsync. ----------
        var stamp = await context.SectorRuleSetStamps.SingleOrDefaultAsync(
            s => s.Id == SectorRuleSetStamp.SingletonId, cancellationToken);
        if (stamp is null)
        {
            stamp = SectorRuleSetStamp.CreateInitial();
            context.SectorRuleSetStamps.Add(stamp);
        }
        stamp.Bump(effectiveActor);
        stamp.SetCatalogHash(ComputeCatalogHash());

        await context.SaveChangesAsync(cancellationToken);

        if (divergences.Count > 0 && logger is not null)
        {
            foreach (var divergence in divergences.Take(50))
                logger.LogWarning("Sector rule reconciliation preserved an admin-managed row: {Divergence}", divergence);
            if (divergences.Count > 50)
                logger.LogWarning("Sector rule reconciliation: {Count} further admin-managed divergences omitted.", divergences.Count - 50);
        }

        return new SectorRuleSeedResult(inserted, updated, skippedExisting, stamp.Version, mode == SeedMode.ForceReset);
    }

    private static void UpsertDefaultSetting(
        MasterDbContext context,
        Dictionary<(string? SegmentCode, string? DomainCode, string SettingKey), SectorDefaultSetting> existingSettings,
        string? segmentCode,
        string? domainCode,
        string settingKey,
        string settingValue,
        string valueType,
        int sortOrder,
        SeedMode mode,
        string actor,
        Action<string> recordDivergence,
        ref int inserted,
        ref int updated,
        ref int skippedExisting)
    {
        var key = (segmentCode, domainCode, settingKey);
        if (existingSettings.TryGetValue(key, out var existing))
        {
            if (mode == SeedMode.InsertMissing)
            {
                skippedExisting++;
            }
            else if (mode == SeedMode.Reconcile && !existing.IsManagedByCatalog)
            {
                recordDivergence($"Setting '{settingKey}' ({segmentCode}/{domainCode}) left unchanged (admin-managed).");
                skippedExisting++;
            }
            else
            {
                existing.ResetFromCatalog(settingValue, valueType, sortOrder);
                existing.MarkCatalogManaged();
                existing.SetAuditInfo(actor, isUpdate: true);
                updated++;
            }
        }
        else
        {
            var created = SectorDefaultSetting.Create(segmentCode, domainCode, settingKey, settingValue, valueType, sortOrder);
            created.SetAuditInfo(actor);
            context.SectorDefaultSettings.Add(created);
            existingSettings[key] = created;
            inserted++;
        }
    }

    /// <summary>Ordinal comparer for the (string?, string?, string) natural key used by default-setting lookups.</summary>
    private sealed class StringTupleComparer : IEqualityComparer<(string? SegmentCode, string? DomainCode, string SettingKey)>
    {
        public static readonly StringTupleComparer Instance = new();

        public bool Equals((string? SegmentCode, string? DomainCode, string SettingKey) x, (string? SegmentCode, string? DomainCode, string SettingKey) y) =>
            string.Equals(x.SegmentCode, y.SegmentCode, StringComparison.Ordinal)
            && string.Equals(x.DomainCode, y.DomainCode, StringComparison.Ordinal)
            && string.Equals(x.SettingKey, y.SettingKey, StringComparison.Ordinal);

        public int GetHashCode((string? SegmentCode, string? DomainCode, string SettingKey) obj) =>
            HashCode.Combine(obj.SegmentCode, obj.DomainCode, obj.SettingKey);
    }
}
