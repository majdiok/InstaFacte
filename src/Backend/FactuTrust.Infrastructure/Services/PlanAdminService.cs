using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services;

/// <summary>Lot C1 — Implémentation EF Core du CRUD plans plateforme.</summary>
public sealed class PlanAdminService : IPlanAdminService
{
    private readonly MasterDbContext _db;
    private readonly ILogger<PlanAdminService> _logger;

    public PlanAdminService(MasterDbContext db, ILogger<PlanAdminService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Clés de limites reconnues par l'enforcement (DbPlanResolver, IPlanQuotaService).
    /// Toute autre clé est stockée mais ne sera jamais consultée → on log un warning à l'écriture.
    /// </summary>
    private static readonly HashSet<string> KnownLimitKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "MaxInvoicesPerMonth", "MaxQuotesPerMonth", "MaxClients",
        "MaxProducts", "MaxStorageBytes", "MaxUsers"
    };

    /// <summary>
    /// Clés de features reconnues par l'enforcement (SubscriptionLimits.HasFeature, DbPlanResolver.HasFeatureAsync).
    /// Toute autre clé est stockée mais ne sera jamais consultée → on log un warning à l'écriture.
    /// </summary>
    private static readonly HashSet<string> KnownFeatureKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ElectronicSignature", "XmlExport", "PaymentTracking", "PrioritySupport"
    };

    /// <summary>
    /// Codes des 3 plans seedés (Free / Monthly / Annual) — alignés sur l'enum
    /// <see cref="SubscriptionPlan"/>. Seuls ces codes peuvent attirer des Subscriptions
    /// historiques sans <c>PlanId</c> renseigné (rétro-compat pré-Lot C1).
    /// </summary>
    private static readonly string[] SeedPlanCodes =
    {
        nameof(SubscriptionPlan.Free),
        nameof(SubscriptionPlan.Monthly),
        nameof(SubscriptionPlan.Annual)
    };

    public async Task<IReadOnlyList<PlanDto>> ListAsync(bool includeArchived, CancellationToken cancellationToken = default)
    {
        var query = _db.Plans
            .AsNoTracking()
            .Include(p => p.Limits)
            .Include(p => p.Features)
            .Include(p => p.Modules)
            .AsQueryable();

        if (!includeArchived)
        {
            query = query.Where(p => p.ArchivedAt == null);
        }

        var plans = await query
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

        // 1) Compteur authoritatif basé sur la FK Subscription.PlanId.
        //    Couvre tous les plans (seed + custom créés via UI admin).
        var planIds = plans.Select(p => p.Id).ToList();
        var countsByPlanId = await _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.PlanId != null && planIds.Contains(s.PlanId!.Value))
            .GroupBy(s => s.PlanId!.Value)
            .Select(g => new { PlanId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PlanId!, x => x.Count, cancellationToken);

        // 2) Rétro-compat : agrège les Subscriptions historiques (PlanId NULL) sur les
        //    3 plans seed. On groupe sur l'enum côté serveur (int) puis on remappe Code côté client
        //    — évite le piège de la traduction EF de Enum.ToString().
        var legacyGroups = await _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.PlanId == null)
            .GroupBy(s => s.Plan)
            .Select(g => new { Plan = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var legacyCountsByCode = legacyGroups
            .Where(g => SeedPlanCodes.Contains(g.Plan.ToString()))
            .ToDictionary(g => g.Plan.ToString(), g => g.Count);

        return plans.Select(p =>
        {
            var byId = countsByPlanId.GetValueOrDefault(p.Id);
            var byLegacy = SeedPlanCodes.Contains(p.Code)
                ? legacyCountsByCode.GetValueOrDefault(p.Code)
                : 0;
            return Map(p, byId + byLegacy);
        }).ToList();
    }

    public async Task<Result<PlanDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await _db.Plans
            .AsNoTracking()
            .Include(p => p.Limits)
            .Include(p => p.Features)
            .Include(p => p.Modules)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (plan is null)
            return Result.Failure<PlanDto>(Error.NotFound(nameof(Plan), id));

        var subsCount = await CountSubscriptionsForPlanAsync(plan, cancellationToken);
        return Result.Success(Map(plan, subsCount));
    }

    public async Task<Result<PlanDto>> CreateAsync(CreatePlanRequest request, CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim();
        if (await _db.Plans.AnyAsync(p => p.Code == code, cancellationToken))
            return Result.Failure<PlanDto>(Error.Validation("Code", "Un plan avec ce code existe déjà."));

        var plan = Plan.Create(
            code,
            request.Name,
            request.Description,
            request.BillingPeriod,
            request.BasePriceTND,
            request.IsPublic,
            request.TrialDays,
            request.SortOrder,
            request.Currency);

        ApplyChildren(plan, request.Limits, request.Features, request.Modules);
        _db.Plans.Add(plan);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(plan, 0));
    }

    public async Task<Result<PlanDto>> UpdateAsync(Guid id, UpdatePlanRequest request, CancellationToken cancellationToken = default)
    {
        var plan = await _db.Plans
            .Include(p => p.Limits)
            .Include(p => p.Features)
            .Include(p => p.Modules)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (plan is null)
            return Result.Failure<PlanDto>(Error.NotFound(nameof(Plan), id));

        plan.Update(
            request.Name,
            request.Description,
            request.IsPublic,
            request.BillingPeriod,
            request.BasePriceTND,
            request.TrialDays,
            request.SortOrder,
            request.Currency);

        ApplyChildren(plan, request.Limits, request.Features, request.Modules);
        await _db.SaveChangesAsync(cancellationToken);

        var subsCount = await CountSubscriptionsForPlanAsync(plan, cancellationToken);
        return Result.Success(Map(plan, subsCount));
    }

    public async Task<Result> ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (plan is null)
            return Result.Failure(Error.NotFound(nameof(Plan), id));

        plan.Archive();
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (plan is null)
            return Result.Failure(Error.NotFound(nameof(Plan), id));

        plan.Reactivate();
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<PlanDto>> CloneAsync(Guid sourceId, ClonePlanRequest request, CancellationToken cancellationToken = default)
    {
        var source = await _db.Plans
            .AsNoTracking()
            .Include(p => p.Limits)
            .Include(p => p.Features)
            .Include(p => p.Modules)
            .FirstOrDefaultAsync(p => p.Id == sourceId, cancellationToken);
        if (source is null)
            return Result.Failure<PlanDto>(Error.NotFound(nameof(Plan), sourceId));

        var newCode = request.NewCode.Trim();
        if (await _db.Plans.AnyAsync(p => p.Code == newCode, cancellationToken))
            return Result.Failure<PlanDto>(Error.Validation("Code", "Un plan avec ce code existe déjà."));

        var clone = Plan.Create(
            newCode,
            request.NewName,
            source.Description,
            source.BillingPeriod,
            source.BasePriceTND,
            isPublic: false, // par défaut le clone est privé jusqu'à validation
            trialDays: source.TrialDays,
            sortOrder: source.SortOrder + 1,
            currency: source.Currency);

        clone.ReplaceLimits(source.Limits.Select(l => (l.Key, l.Value)));
        clone.ReplaceFeatures(source.Features.Select(f => (f.FeatureKey, f.Enabled)));
        clone.ReplaceModules(source.Modules.Select(m => (m.Module, m.IsIncluded)));

        _db.Plans.Add(clone);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(clone, 0));
    }

    // ===== Helpers ============================================================

    /// <summary>
    /// Calcule le nombre de Subscriptions attachées à un plan :
    /// <list type="bullet">
    ///   <item>Subscriptions ayant <c>PlanId == plan.Id</c> (source de vérité, plans seed + custom).</item>
    ///   <item>+ Subscriptions historiques (<c>PlanId == null</c>) dont l'enum <c>Plan.ToString()</c>
    ///         correspond au Code du plan — uniquement pour les 3 codes seed (Free/Monthly/Annual).</item>
    /// </list>
    /// </summary>
    private async Task<int> CountSubscriptionsForPlanAsync(Plan plan, CancellationToken cancellationToken)
    {
        var byId = await _db.Subscriptions
            .AsNoTracking()
            .CountAsync(s => s.PlanId == plan.Id, cancellationToken);

        if (!SeedPlanCodes.Contains(plan.Code))
            return byId;

        // Conversion safe : on ne rentre ici que pour les 3 codes seed connus de l'enum.
        var planEnum = Enum.Parse<SubscriptionPlan>(plan.Code);
        var byLegacy = await _db.Subscriptions
            .AsNoTracking()
            .CountAsync(
                s => s.PlanId == null && s.Plan == planEnum,
                cancellationToken);

        return byId + byLegacy;
    }

    private void ApplyChildren(
        Plan plan,
        IReadOnlyList<PlanLimitDto> limits,
        IReadOnlyList<PlanFeatureDto> features,
        IReadOnlyList<PlanModuleDto> modules)
    {
        if (limits is { Count: > 0 })
        {
            foreach (var l in limits.Where(l => !string.IsNullOrWhiteSpace(l.Key) && !KnownLimitKeys.Contains(l.Key)))
            {
                _logger.LogWarning(
                    "Plan {Code}: clé de limite inconnue « {Key} » — sera stockée mais non enforced. " +
                    "Clés reconnues : {Known}.",
                    plan.Code, l.Key, string.Join(", ", KnownLimitKeys));
            }
            plan.ReplaceLimits(limits.Select(l => (l.Key, l.Value)));
        }
        if (features is { Count: > 0 })
        {
            foreach (var f in features.Where(f => !string.IsNullOrWhiteSpace(f.FeatureKey) && !KnownFeatureKeys.Contains(f.FeatureKey)))
            {
                _logger.LogWarning(
                    "Plan {Code}: clé de feature inconnue « {Key} » — sera stockée mais non enforced. " +
                    "Clés reconnues : {Known}.",
                    plan.Code, f.FeatureKey, string.Join(", ", KnownFeatureKeys));
            }
            plan.ReplaceFeatures(features.Select(f => (f.FeatureKey, f.Enabled)));
        }
        if (modules is { Count: > 0 })
        {
            // Les modules cœur sont inclus dans tout plan par définition (assistant d'inscription
            // « Inclus dans votre espace », badge « Cœur », ValidateCoreModules qui refuse de les
            // désactiver). Décocher l'un d'eux dans le back-office produisait un plan qui, jusqu'à
            // ce correctif, privait les nouveaux espaces de clients, produits, ventes et trésorerie.
            // On corrige la valeur et on journalise — même idiome que les clés inconnues ci-dessus,
            // aucune édition légitime n'est bloquée.
            var coreModules = SectorConfigurationCatalog.CoreModules.Select(m => (int)m).ToHashSet();
            var forcedCore = modules
                .Where(m => coreModules.Contains(m.Module) && !m.IsIncluded)
                .Select(m => m.Module)
                .ToList();

            if (forcedCore.Count > 0)
            {
                _logger.LogWarning(
                    "Plan {Code} : {Count} module(s) cœur reçus décochés — forcés à inclus (un plan ne peut pas exclure un module cœur). Modules : {Modules}.",
                    plan.Code,
                    forcedCore.Count,
                    string.Join(", ", forcedCore.Select(id => ((AppModule)id).ToDisplayString())));
            }

            plan.ReplaceModules(modules.Select(m => (m.Module, m.IsIncluded || coreModules.Contains(m.Module))));
        }
    }

    private static PlanDto Map(Plan p, int subsCount) => new()
    {
        Id = p.Id,
        Code = p.Code,
        Name = p.Name,
        Description = p.Description,
        IsPublic = p.IsPublic,
        IsActive = p.IsActive,
        BillingPeriod = p.BillingPeriod,
        BillingPeriodDisplay = p.BillingPeriod.ToDisplayString(),
        BasePriceTND = p.BasePriceTND,
        Currency = p.Currency,
        TrialDays = p.TrialDays,
        SortOrder = p.SortOrder,
        ArchivedAt = p.ArchivedAt,
        Limits = p.Limits.Select(l => new PlanLimitDto { Key = l.Key, Value = l.Value }).ToList(),
        Features = p.Features.Select(f => new PlanFeatureDto { FeatureKey = f.FeatureKey, Enabled = f.Enabled }).ToList(),
        Modules = p.Modules.Select(m => new PlanModuleDto
        {
            Module = m.Module,
            ModuleDisplay = TryDisplayModule(m.Module),
            IsIncluded = m.IsIncluded
        }).ToList(),
        SubscriptionsCount = subsCount
    };

    private static string TryDisplayModule(int module)
    {
        return Enum.IsDefined(typeof(AppModule), module)
            ? ((AppModule)module).ToDisplayString()
            : $"Module #{module}";
    }
}
