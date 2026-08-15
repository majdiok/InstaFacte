using FactuTrust.Application.Common.Interfaces.Treasury;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.Treasury.Collectors;

/// <summary>
/// Encaissements attendus sur les factures clients non soldées.
/// </summary>
/// <remarks>
/// <para>
/// Deux décalages sont appliqués à l'échéance contractuelle : le retard médian **observé pour ce
/// client précisément**, et à défaut celui de la société. Projeter à l'échéance contractuelle
/// donnerait un prévisionnel systématiquement optimiste — c'est le défaut le plus courant des
/// tableaux de trésorerie faits à la main.
/// </para>
/// <para>
/// Pas de double comptage avec les traites : un règlement par traite est enregistré comme
/// <c>Payment</c>, il diminue donc déjà le reste dû calculé ici, et
/// <see cref="ClientEffetCollector"/> le reprend à sa date d'échéance propre.
/// </para>
/// </remarks>
public sealed class ClientReceivablesCollector : ICashFlowSourceCollector
{
    private readonly ITenantDbContextFactory _contextFactory;

    public ClientReceivablesCollector(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public CashFlowSourceType SourceType => CashFlowSourceType.ClientInvoice;

    public bool IsEnabled(TreasuryForecastOptions options) => true;

    /// <summary>Statuts d'une facture susceptible de donner lieu à un encaissement.</summary>
    private static readonly InvoiceStatus[] OpenStatuses =
    {
        InvoiceStatus.Validated,
        InvoiceStatus.Signed,
        InvoiceStatus.PartiallyPaid,
        InvoiceStatus.Overdue
    };

    public async Task<IReadOnlyList<CashFlowLineDraft>> CollectAsync(
        CashFlowCollectionContext context,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        var invoices = await ctx.Invoices
            .AsNoTracking()
            .Where(i => OpenStatuses.Contains(i.Status)
                        && i.Type != InvoiceType.CreditNote
                        && i.CancelledAt == null)
            .Select(i => new
            {
                i.Id,
                Number = i.Number.Value,
                i.IssueDate,
                i.DueDate,
                i.ClientId,
                Total = i.TotalAmount.Amount
            })
            .ToListAsync(cancellationToken);

        if (invoices.Count == 0)
            return Array.Empty<CashFlowLineDraft>();

        var invoiceIds = invoices.Select(i => i.Id).ToList();

        // Somme réglée par facture, retenue à la source comprise : c'est la définition retenue par
        // Payment.GetTotalAppliedTowardInvoice, à laquelle Invoice.ReconcilePaymentStatus se fie.
        var paidByInvoice = await ctx.Payments
            .AsNoTracking()
            .Where(p => invoiceIds.Contains(p.InvoiceId) && !p.IsRefunded)
            .GroupBy(p => p.InvoiceId)
            .Select(g => new
            {
                InvoiceId = g.Key,
                Paid = g.Sum(p => p.Amount.Amount + (p.ClientWithholdingAmount ?? 0m))
            })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Paid, cancellationToken);

        var clientNames = await LoadClientNamesAsync(
            ctx,
            invoices.Select(i => i.ClientId).Distinct().ToList(),
            cancellationToken);

        var delays = await PaymentDelayStatistics.LoadAsync(ctx, context, cancellationToken);

        var defaultTermDays = await ResolveDefaultTermDaysAsync(ctx, cancellationToken);

        var drafts = new List<CashFlowLineDraft>();

        foreach (var invoice in invoices)
        {
            var paid = paidByInvoice.TryGetValue(invoice.Id, out var p) ? p : 0m;
            var remaining = MillimeRounding.Round(invoice.Total - paid);
            if (remaining <= 0m) continue;

            // Une facture sans échéance suit les conditions de paiement par défaut du référentiel :
            // la rattacher à sa date d'émission la ferait apparaître comme immédiatement encaissable.
            var contractualDate = invoice.DueDate?.Date ?? invoice.IssueDate.Date.AddDays(defaultTermDays);

            var shiftDays = delays.ResolveShiftDays(invoice.ClientId);
            var expectedDate = contractualDate.AddDays(shiftDays);

            // Une échéance déjà dépassée ne peut pas être projetée dans le passé : le règlement
            // est attendu au plus tôt aujourd'hui.
            if (expectedDate < context.Today)
                expectedDate = context.Today;

            if (expectedDate > context.To) continue;

            var daysOverdue = (int)(context.Today - contractualDate).TotalDays;
            var probability = context.Options.ReceivableProbability.Resolve(daysOverdue);

            drafts.Add(new CashFlowLineDraft
            {
                Direction = CashFlowDirection.Inflow,
                SourceType = CashFlowSourceType.ClientInvoice,
                SourceId = invoice.Id,
                SourceReference = invoice.Number,
                Label = $"Facture {invoice.Number}",
                ThirdPartyName = clientNames.TryGetValue(invoice.ClientId, out var name) ? name : null,
                ContractualDate = contractualDate,
                ExpectedDate = expectedDate,
                Amount = remaining,
                ProbabilityPercent = probability,
                IsConfirmed = false
            });
        }

        return drafts;
    }

    private static async Task<Dictionary<Guid, string>> LoadClientNamesAsync(
        Persistence.TenantDbContext ctx,
        IReadOnlyCollection<Guid> clientIds,
        CancellationToken cancellationToken)
    {
        if (clientIds.Count == 0) return new Dictionary<Guid, string>();

        return await ctx.Clients
            .AsNoTracking()
            .Where(c => clientIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
    }

    /// <summary>
    /// Délai de paiement par défaut, repris du modèle de conditions marqué par défaut. 30 jours à
    /// défaut de référentiel — la valeur usuelle en Tunisie.
    /// </summary>
    private static async Task<int> ResolveDefaultTermDaysAsync(
        Persistence.TenantDbContext ctx,
        CancellationToken cancellationToken)
    {
        var template = await ctx.PaymentTermTemplates
            .AsNoTracking()
            .Where(t => t.IsDefault)
            .Select(t => (int?)t.DelayDays)
            .FirstOrDefaultAsync(cancellationToken);

        return template is > 0 ? template.Value : 30;
    }
}
