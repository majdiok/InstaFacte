using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Plan §3.3 — explainable module usage recommendations. Each rule is independent, small, and
/// documented inline; a module already enabled or already dismissed by the tenant is never
/// suggested (idempotent: calling <see cref="GetRecommendationsAsync"/> repeatedly is safe and a
/// dismissal is permanent for that tenant/module pair).
/// </summary>
public sealed class ModuleUsageRecommendationService : IModuleUsageRecommendationService
{
    private readonly MasterDbContext _master;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantDbContextFactory _tenantDbFactory;
    private readonly ModuleRecommendationsOptions _options;

    public ModuleUsageRecommendationService(
        MasterDbContext master,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        ITenantDbContextFactory tenantDbFactory,
        IOptions<ModuleRecommendationsOptions> options)
    {
        _master = master;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _tenantDbFactory = tenantDbFactory;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<ModuleRecommendationDto>> GetRecommendationsAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return Array.Empty<ModuleRecommendationDto>();

        var tenantId = _tenantContext.TenantId;
        if (tenantId is null)
            return Array.Empty<ModuleRecommendationDto>();

        var enabledModules = await ComputeEnabledModuleSetAsync(_currentUser.UserId, cancellationToken);

        var dismissedIds = await _master.ModuleRecommendationDismissals.AsNoTracking()
            .Where(d => d.TenantId == tenantId.Value)
            .Select(d => d.Module)
            .ToListAsync(cancellationToken);
        var dismissedSet = new HashSet<int>(dismissedIds);

        var results = new List<ModuleRecommendationDto>();
        var sinceUtc = DateTime.UtcNow.AddDays(-Math.Max(1, _options.LookbackDays));

        await using var tenantDb = _tenantDbFactory.CreateContext();

        // Rule 1 — many quotes issued but CRM inactive: prospect follow-up would benefit from CRM.
        if (!enabledModules.Contains(AppModule.CRM) && !dismissedSet.Contains((int)AppModule.CRM))
        {
            var quoteCount = await tenantDb.Quotes.AsNoTracking()
                .CountAsync(q => q.IssueDate >= sinceUtc, cancellationToken);
            if (quoteCount >= _options.MinQuotesPerMonthForCrm)
            {
                results.Add(new ModuleRecommendationDto
                {
                    ModuleId = (int)AppModule.CRM,
                    ReasonCode = "high-quote-volume",
                    ReasonFr = $"Vous avez émis {quoteCount} devis au cours des {_options.LookbackDays} derniers jours. " +
                        "Le module CRM Commercial vous aide à suivre vos prospects et vos relances."
                });
            }
        }

        // Rule 2 — many delivery notes issued but Stock inactive: stock tracking would help.
        if (!enabledModules.Contains(AppModule.Stock) && !dismissedSet.Contains((int)AppModule.Stock))
        {
            var deliveryNoteCount = await tenantDb.DeliveryNotes.AsNoTracking()
                .CountAsync(d => d.IssueDate >= sinceUtc, cancellationToken);
            if (deliveryNoteCount >= _options.MinDeliveryNotesPerMonthForStock)
            {
                results.Add(new ModuleRecommendationDto
                {
                    ModuleId = (int)AppModule.Stock,
                    ReasonCode = "high-delivery-note-volume",
                    ReasonFr = $"Vous avez émis {deliveryNoteCount} bons de livraison au cours des {_options.LookbackDays} derniers jours. " +
                        "Le module Stock vous aide à suivre vos quantités et vos entrepôts."
                });
            }
        }

        // Rule 3 — high, regular invoicing volume but RecurringContracts inactive: billing
        // automation for recurring/subscription-like invoicing would save time.
        if (!enabledModules.Contains(AppModule.RecurringContracts) && !dismissedSet.Contains((int)AppModule.RecurringContracts))
        {
            var invoiceCount = await tenantDb.Invoices.AsNoTracking()
                .CountAsync(i => i.IssueDate >= sinceUtc, cancellationToken);
            if (invoiceCount >= _options.MinInvoicesPerMonthForRecurringContracts)
            {
                results.Add(new ModuleRecommendationDto
                {
                    ModuleId = (int)AppModule.RecurringContracts,
                    ReasonCode = "high-recurring-invoice-volume",
                    ReasonFr = $"Vous avez émis {invoiceCount} factures au cours des {_options.LookbackDays} derniers jours. " +
                        "Le module Contrats récurrents peut automatiser la facturation de vos abonnements réguliers."
                });
            }
        }

        return results;
    }

    public async Task<Result<bool>> DismissAsync(int moduleId, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(AppModule), moduleId))
            return Result.Failure<bool>(Error.Validation("ModuleId", "Module invalide."));

        var tenantId = _tenantContext.TenantId;
        if (tenantId is null)
            return Result.Failure<bool>(Error.Unauthorized("Contexte tenant introuvable."));

        var alreadyDismissed = await _master.ModuleRecommendationDismissals
            .AnyAsync(d => d.TenantId == tenantId.Value && d.Module == moduleId, cancellationToken);
        if (!alreadyDismissed)
        {
            _master.ModuleRecommendationDismissals.Add(
                ModuleRecommendationDismissal.Create(tenantId.Value, moduleId, _currentUser.UserId));
            await _master.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(true);
    }

    /// <summary>
    /// Current enabled module set for a user: no grant rows ⇒ all modules (legacy canonical form),
    /// otherwise the modules with <c>IsEnabled=true</c>. Mirrors
    /// <c>CompanyModulesController.ComputeEnabledModuleSetAsync</c>.
    /// </summary>
    private async Task<HashSet<AppModule>> ComputeEnabledModuleSetAsync(Guid? userId, CancellationToken cancellationToken)
    {
        if (userId is null)
            return new HashSet<AppModule>(AppModuleExtensions.AllValues);

        var grants = await _master.UserModuleGrants.AsNoTracking()
            .Where(g => g.UserId == userId.Value)
            .ToListAsync(cancellationToken);

        if (grants.Count == 0)
            return new HashSet<AppModule>(AppModuleExtensions.AllValues);

        return grants.Where(g => g.IsEnabled).Select(g => g.Module).ToHashSet();
    }
}
