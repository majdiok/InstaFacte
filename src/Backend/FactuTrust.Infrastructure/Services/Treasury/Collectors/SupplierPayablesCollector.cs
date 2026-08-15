using FactuTrust.Application.Common.Interfaces.Treasury;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.Treasury.Collectors;

/// <summary>
/// Décaissements attendus sur les factures fournisseurs non soldées.
/// </summary>
/// <remarks>
/// <para>
/// Le montant retenu est le <b>net après retenue à la source</b> quand la facture y est soumise :
/// c'est la somme qui sort effectivement de la banque, la retenue étant reversée séparément à
/// l'État via l'échéancier fiscal (<see cref="FiscalObligationsCollector"/>). Retenir le montant
/// brut ferait compter deux fois la retenue.
/// </para>
/// <para>
/// Aucun décalage d'usage n'est appliqué, contrairement aux créances clients : la société décide
/// de ses propres règlements. Projeter ses dettes en retard reviendrait à budgéter un défaut de
/// paiement.
/// </para>
/// </remarks>
public sealed class SupplierPayablesCollector : ICashFlowSourceCollector
{
    private readonly ITenantDbContextFactory _contextFactory;

    public SupplierPayablesCollector(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public CashFlowSourceType SourceType => CashFlowSourceType.SupplierInvoice;

    public bool IsEnabled(TreasuryForecastOptions options) => true;

    private static readonly SupplierInvoiceStatus[] OpenStatuses =
    {
        SupplierInvoiceStatus.Pending,
        SupplierInvoiceStatus.PartiallyPaid
    };

    public async Task<IReadOnlyList<CashFlowLineDraft>> CollectAsync(
        CashFlowCollectionContext context,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        var invoices = await ctx.SupplierInvoices
            .AsNoTracking()
            .Where(i => OpenStatuses.Contains(i.Status) && i.CancelledAt == null)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.DueDate,
                i.SupplierId,
                Total = i.TotalAmount.Amount,
                i.IsSubjectToWithholding,
                i.NetAmountAfterWithholding
            })
            .ToListAsync(cancellationToken);

        if (invoices.Count == 0)
            return Array.Empty<CashFlowLineDraft>();

        var invoiceIds = invoices.Select(i => i.Id).ToList();

        var paidByInvoice = await ctx.SupplierPayments
            .AsNoTracking()
            .Where(p => invoiceIds.Contains(p.SupplierInvoiceId))
            .GroupBy(p => p.SupplierInvoiceId)
            .Select(g => new { InvoiceId = g.Key, Paid = g.Sum(p => p.Amount.Amount) })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Paid, cancellationToken);

        var supplierIds = invoices.Select(i => i.SupplierId).Distinct().ToList();
        var supplierNames = await ctx.Suppliers
            .AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var drafts = new List<CashFlowLineDraft>();

        foreach (var invoice in invoices)
        {
            var payable = invoice.IsSubjectToWithholding && invoice.NetAmountAfterWithholding.HasValue
                ? invoice.NetAmountAfterWithholding.Value
                : invoice.Total;

            var paid = paidByInvoice.TryGetValue(invoice.Id, out var p) ? p : 0m;
            var remaining = MillimeRounding.Round(payable - paid);
            if (remaining <= 0m) continue;

            var contractualDate = invoice.DueDate.Date;

            // Une dette déjà échue est à régler sans délai : elle pèse sur la trésorerie du jour,
            // pas sur celle de son échéance passée.
            var expectedDate = contractualDate < context.Today ? context.Today : contractualDate;
            if (expectedDate > context.To) continue;

            drafts.Add(new CashFlowLineDraft
            {
                Direction = CashFlowDirection.Outflow,
                SourceType = CashFlowSourceType.SupplierInvoice,
                SourceId = invoice.Id,
                SourceReference = invoice.InvoiceNumber,
                Label = $"Facture fournisseur {invoice.InvoiceNumber}",
                ThirdPartyName = supplierNames.TryGetValue(invoice.SupplierId, out var name) ? name : null,
                ContractualDate = contractualDate,
                ExpectedDate = expectedDate,
                Amount = remaining,
                ProbabilityPercent = 100m,
                IsConfirmed = true
            });
        }

        return drafts;
    }
}
