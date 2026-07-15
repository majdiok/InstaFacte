using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.Background;

/// <summary>
/// Lot C6 — Job Hangfire quotidien : scanne les abonnements arrivant à échéance et
/// génère un brouillon de facture de renouvellement.
///
/// Idempotence : on ne crée pas de brouillon si une facture <c>Subscription</c> émise
/// existe déjà pour la période courante (jointure sur <c>PeriodFrom/PeriodTo</c>).
///
/// Programmé via <c>RecurringJob.AddOrUpdate</c> à 04h UTC quotidien.
/// </summary>
public sealed class RenewalScanJob
{
    /// <summary>Fenêtre de scan : génère un brouillon J-3 avant l'expiration.</summary>
    private static readonly TimeSpan WindowAhead = TimeSpan.FromDays(3);

    private readonly MasterDbContext _db;
    private readonly IPlatformInvoiceAdminService _invoiceAdmin;
    private readonly ILogger<RenewalScanJob> _logger;

    public RenewalScanJob(
        MasterDbContext db,
        IPlatformInvoiceAdminService invoiceAdmin,
        ILogger<RenewalScanJob> logger)
    {
        _db = db;
        _invoiceAdmin = invoiceAdmin;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var threshold = nowUtc.Add(WindowAhead);

        var dueSubscriptions = await _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.Plan != SubscriptionPlan.Free
                && s.Status == SubscriptionStatus.Active
                && s.EndDate.HasValue
                && s.EndDate.Value <= threshold)
            .ToListAsync(cancellationToken);

        if (dueSubscriptions.Count == 0)
        {
            _logger.LogInformation("RenewalScanJob: no subscriptions due in next {Days} days", WindowAhead.Days);
            return;
        }

        var created = 0;
        var skipped = 0;
        foreach (var s in dueSubscriptions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var nextPeriodStart = s.EndDate ?? nowUtc;
            var nextPeriodEnd = s.Plan == SubscriptionPlan.Annual
                ? nextPeriodStart.AddYears(1)
                : nextPeriodStart.AddMonths(1);

            // Idempotence : skip si une facture Subscription pour cette période existe déjà.
            var alreadyInvoiced = await _db.PlatformInvoices.AnyAsync(i =>
                i.TenantId == s.TenantId
                && i.BillingType == PlatformInvoiceBillingType.Subscription
                && i.PeriodFrom == nextPeriodStart
                && i.Status != PlatformInvoiceStatus.Cancelled,
                cancellationToken);

            if (alreadyInvoiced)
            {
                skipped++;
                continue;
            }

            var amountTnd = s.Plan switch
            {
                SubscriptionPlan.Annual => s.AnnualPrice?.Amount ?? 468m,
                SubscriptionPlan.Monthly => s.MonthlyPrice?.Amount ?? 49m,
                _ => 0m
            };
            if (amountTnd <= 0)
            {
                skipped++;
                continue;
            }

            var description = s.Plan == SubscriptionPlan.Annual
                ? "Renouvellement annuel — abonnement FactuTrust"
                : "Renouvellement mensuel — abonnement FactuTrust";

            var draftRequest = new CreatePlatformInvoiceRequest
            {
                TenantId = s.TenantId,
                BillingType = PlatformInvoiceBillingType.Subscription,
                InvoiceDate = nowUtc.Date,
                DueDate = nowUtc.Date.AddDays(7),
                PeriodFrom = nextPeriodStart,
                PeriodTo = nextPeriodEnd,
                DiscountAmount = 0m,
                CreditsApplied = 0m,
                Lines = new[]
                {
                    new CreatePlatformInvoiceLineRequest
                    {
                        Description = description,
                        Quantity = 1,
                        UnitPriceHT = amountTnd,
                        VatRate = 19m
                    }
                }
            };

            // Acteur = système (Guid.Empty)
            var draftResult = await _invoiceAdmin.CreateDraftAsync(draftRequest, Guid.Empty, cancellationToken);
            if (draftResult.IsFailure)
            {
                _logger.LogWarning("RenewalScan: draft for tenant {Tenant} failed: {Err}", s.TenantId, draftResult.Error.Description);
                continue;
            }

            // Émet immédiatement (numéro fiscal alloué dès aujourd'hui).
            var issued = await _invoiceAdmin.IssueAsync(draftResult.Value.Id, cancellationToken);
            if (issued.IsFailure)
            {
                _logger.LogWarning("RenewalScan: issue for tenant {Tenant} failed: {Err}", s.TenantId, issued.Error.Description);
                continue;
            }
            created++;
        }

        _logger.LogInformation("RenewalScanJob: scanned {Total}, created {Created}, skipped {Skipped}",
            dueSubscriptions.Count, created, skipped);
    }
}
