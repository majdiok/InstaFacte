using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionController : ControllerBase
{
    private readonly MasterDbContext _masterContext;
    private readonly ITenantContext _tenantContext;
    private readonly ISubscriptionAdminService _subscriptionAdmin;

    public SubscriptionController(
        MasterDbContext masterContext,
        ITenantContext tenantContext,
        ISubscriptionAdminService subscriptionAdmin)
    {
        _masterContext = masterContext;
        _tenantContext = tenantContext;
        _subscriptionAdmin = subscriptionAdmin;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SettingsRead)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubscription(CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(ApiResponse<SubscriptionDto>.Fail("Aucun contexte d'entreprise disponible"));

        var subscription = await _masterContext.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value, cancellationToken);

        if (subscription is null)
            return NotFound(ApiResponse<SubscriptionDto>.Fail("Aucun abonnement trouvé"));

        subscription.ResetMonthlyCounter();
        subscription.CheckExpiration();

        var dto = SubscriptionDtoMapper.ToDto(subscription);
        return Ok(ApiResponse<SubscriptionDto>.Ok(dto));
    }

    [HttpGet("plans")]
    [Authorize(Policy = PermissionPolicies.SettingsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubscriptionPlanOptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailablePlans(CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(ApiResponse<IReadOnlyList<SubscriptionPlanOptionDto>>.Fail("Aucun contexte d'entreprise disponible"));

        var currentSub = await _masterContext.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value, cancellationToken);

        var currentPlan = currentSub?.Plan ?? SubscriptionPlan.Free;

        var plans = new List<SubscriptionPlanOptionDto>
        {
            new()
            {
                Id = "Free",
                Name = "Gratuit",
                PriceMonthly = 0,
                Currency = "TND",
                Period = "mois",
                Features = new[]
                {
                    "10 factures par mois",
                    "10 devis par mois",
                    "Jusqu'à 20 clients",
                    "Jusqu'à 50 produits ou services",
                    "100 Mo de stockage documents",
                    "Jusqu'à 5 utilisateurs",
                    "Export PDF (factures et devis)",
                    "Découverte des modules FactuTrust (sous quotas)"
                },
                DisabledFeatures = new[]
                {
                    "Export XML / dossiers TEJ avancés",
                    "Signature électronique",
                    "Suivi des paiements",
                    "Archivage sécurisé étendu",
                    "Support prioritaire"
                },
                IsCurrent = currentPlan == SubscriptionPlan.Free,
                Limits = new PlanLimitsDto
                {
                    MaxInvoicesPerMonth = SubscriptionLimits.Free.MaxInvoicesPerMonth,
                    MaxQuotesPerMonth = SubscriptionLimits.Free.MaxQuotesPerMonth,
                    MaxClients = SubscriptionLimits.Free.MaxClients,
                    MaxProducts = SubscriptionLimits.Free.MaxProducts,
                    MaxStorageBytes = SubscriptionLimits.Free.MaxStorageBytes,
                    ElectronicSignature = SubscriptionLimits.Free.ElectronicSignature,
                    XmlExport = SubscriptionLimits.Free.XmlExport,
                    PaymentTracking = SubscriptionLimits.Free.PaymentTracking,
                    PrioritySupport = SubscriptionLimits.Free.PrioritySupport,
                    IsUnlimited = false
                }
            },
            new()
            {
                Id = "Monthly",
                Name = "Mensuel",
                PriceMonthly = 49m,
                Currency = "TND",
                Period = "mois",
                Features = new[]
                {
                    "Factures, devis, clients et produits illimités",
                    "Achats, stock multi-entrepôts, trésorerie & banques",
                    "Comptabilité, CRM, fiscal / TEJ",
                    "Assistant IA — chat contextuel, OCR, recommandations",
                    "Prévisions intelligentes (forecasting)",
                    "Signature électronique",
                    "Export PDF et export XML TEJ",
                    "Suivi des paiements et rapports avancés",
                    "Archivage sécurisé",
                    "5 Go de stockage inclus"
                },
                DisabledFeatures = new[]
                {
                    "Support prioritaire"
                },
                IsCurrent = currentPlan == SubscriptionPlan.Monthly,
                Limits = new PlanLimitsDto
                {
                    MaxInvoicesPerMonth = SubscriptionLimits.Monthly.MaxInvoicesPerMonth,
                    MaxQuotesPerMonth = SubscriptionLimits.Monthly.MaxQuotesPerMonth,
                    MaxClients = SubscriptionLimits.Monthly.MaxClients,
                    MaxProducts = SubscriptionLimits.Monthly.MaxProducts,
                    MaxStorageBytes = SubscriptionLimits.Monthly.MaxStorageBytes,
                    ElectronicSignature = SubscriptionLimits.Monthly.ElectronicSignature,
                    XmlExport = SubscriptionLimits.Monthly.XmlExport,
                    PaymentTracking = SubscriptionLimits.Monthly.PaymentTracking,
                    PrioritySupport = SubscriptionLimits.Monthly.PrioritySupport,
                    IsUnlimited = true
                }
            },
            new()
            {
                Id = "Annual",
                Name = "Annuel",
                PriceMonthly = 39m,
                PriceAnnual = 468m,
                Currency = "TND",
                Period = "mois",
                Features = new[]
                {
                    "Factures, devis, clients et produits illimités",
                    "Achats, stock multi-entrepôts, trésorerie & banques",
                    "Comptabilité, CRM, fiscal / TEJ",
                    "Assistant IA — chat contextuel, OCR, recommandations",
                    "Prévisions intelligentes (forecasting)",
                    "Signature électronique",
                    "Export PDF et export XML TEJ",
                    "Suivi des paiements et rapports avancés",
                    "Archivage sécurisé",
                    "20 Go de stockage inclus",
                    "Support prioritaire"
                },
                DisabledFeatures = Array.Empty<string>(),
                IsPopular = true,
                IsCurrent = currentPlan == SubscriptionPlan.Annual,
                SavePercentage = 20,
                Limits = new PlanLimitsDto
                {
                    MaxInvoicesPerMonth = SubscriptionLimits.Annual.MaxInvoicesPerMonth,
                    MaxQuotesPerMonth = SubscriptionLimits.Annual.MaxQuotesPerMonth,
                    MaxClients = SubscriptionLimits.Annual.MaxClients,
                    MaxProducts = SubscriptionLimits.Annual.MaxProducts,
                    MaxStorageBytes = SubscriptionLimits.Annual.MaxStorageBytes,
                    ElectronicSignature = SubscriptionLimits.Annual.ElectronicSignature,
                    XmlExport = SubscriptionLimits.Annual.XmlExport,
                    PaymentTracking = SubscriptionLimits.Annual.PaymentTracking,
                    PrioritySupport = SubscriptionLimits.Annual.PrioritySupport,
                    IsUnlimited = true
                }
            }
        };

        return Ok(ApiResponse<IReadOnlyList<SubscriptionPlanOptionDto>>.Ok(plans));
    }

    [HttpPost("change")]
    [Authorize(Policy = PermissionPolicies.SettingsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePlan(
        [FromBody] ChangePlanRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(ApiResponse<SubscriptionDto>.Fail("Aucun contexte d'entreprise disponible"));

        if (!Enum.TryParse<SubscriptionPlan>(request.Plan, ignoreCase: true, out var newPlan))
            return BadRequest(ApiResponse<SubscriptionDto>.Fail("Plan invalide. Valeurs acceptées : Free, Monthly, Annual"));

        var changeResult = await _subscriptionAdmin.ChangePlanAsync(tenantId.Value, newPlan, cancellationToken);
        if (changeResult.IsFailure)
            return BadRequest(ApiResponse<SubscriptionDto>.Fail(changeResult.Error.Description));

        var dto = SubscriptionDtoMapper.ToDto(changeResult.Value);
        return Ok(ApiResponse<SubscriptionDto>.Ok(dto, "Forfait mis à jour avec succès"));
    }

    [HttpPost("cancel")]
    [Authorize(Policy = PermissionPolicies.SettingsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelSubscription(
        [FromBody] CancelSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(ApiResponse<SubscriptionDto>.Fail("Aucun contexte d'entreprise disponible"));

        var cancelResult = await _subscriptionAdmin.CancelAsync(tenantId.Value, request.Reason, cancellationToken);
        if (cancelResult.IsFailure)
        {
            if (cancelResult.Error.Code == "Subscription.NotFound")
                return NotFound(ApiResponse<SubscriptionDto>.Fail(cancelResult.Error.Description));
            return BadRequest(ApiResponse<SubscriptionDto>.Fail(cancelResult.Error.Description));
        }

        var dto = SubscriptionDtoMapper.ToDto(cancelResult.Value);
        return Ok(ApiResponse<SubscriptionDto>.Ok(dto, "Abonnement annulé"));
    }
}
