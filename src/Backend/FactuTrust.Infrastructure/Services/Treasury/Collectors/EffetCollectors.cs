using FactuTrust.Application.Common.Interfaces.Treasury;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.Treasury.Collectors;

/// <summary>
/// Encaissements portés par les effets de commerce clients (traites) encore en portefeuille.
/// </summary>
/// <remarks>
/// Ce sont les flux les mieux datés du prévisionnel : la date d'échéance est contractuelle et ne
/// subit aucun décalage d'usage. Seul le risque d'impayé justifie une probabilité inférieure à
/// 100 % — d'où <c>ClientEffetProbabilityPercent</c>.
/// Les effets déjà encaissés (<see cref="EffetStatus.Encaisse"/>) ou impayés
/// (<see cref="EffetStatus.Impaye"/>) sont exclus : le premier a produit son flux, le second est
/// redevenu une créance ordinaire portée par <see cref="ClientReceivablesCollector"/>.
/// </remarks>
public sealed class ClientEffetCollector : ICashFlowSourceCollector
{
    private readonly ITenantDbContextFactory _contextFactory;

    public ClientEffetCollector(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public CashFlowSourceType SourceType => CashFlowSourceType.ClientEffet;

    public bool IsEnabled(TreasuryForecastOptions options) => true;

    public async Task<IReadOnlyList<CashFlowLineDraft>> CollectAsync(
        CashFlowCollectionContext context,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        var rows = await ctx.Payments
            .AsNoTracking()
            .Where(p => p.EffetStatus == EffetStatus.EnPortefeuille
                        && p.EffetDueDate != null
                        && !p.IsRefunded
                        && p.EffetDueDate >= context.From
                        && p.EffetDueDate <= context.To)
            .Join(
                ctx.Invoices.AsNoTracking(),
                payment => payment.InvoiceId,
                invoice => invoice.Id,
                (payment, invoice) => new
                {
                    payment.Id,
                    DueDate = payment.EffetDueDate!.Value,
                    Amount = payment.Amount.Amount,
                    payment.Reference,
                    InvoiceNumber = invoice.Number.Value,
                    invoice.ClientId
                })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return Array.Empty<CashFlowLineDraft>();

        var clientIds = rows.Select(r => r.ClientId).Distinct().ToList();
        var clientNames = await ctx.Clients
            .AsNoTracking()
            .Where(c => clientIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        return rows
            .Where(r => r.Amount > 0m)
            .Select(r => new CashFlowLineDraft
            {
                Direction = CashFlowDirection.Inflow,
                SourceType = CashFlowSourceType.ClientEffet,
                SourceId = r.Id,
                SourceReference = r.Reference ?? r.InvoiceNumber,
                Label = $"Traite {r.Reference ?? r.InvoiceNumber}",
                ThirdPartyName = clientNames.TryGetValue(r.ClientId, out var name) ? name : null,
                ContractualDate = r.DueDate.Date,
                ExpectedDate = r.DueDate.Date,
                Amount = r.Amount,
                ProbabilityPercent = context.Options.ClientEffetProbabilityPercent,
                IsConfirmed = true
            })
            .ToList();
    }
}

/// <summary>
/// Décaissements portés par les effets de commerce fournisseurs à échoir.
/// </summary>
/// <remarks>
/// Probabilité de 100 % : une traite acceptée est un engagement ferme de la société. Contrairement
/// aux encaissements, il n'y a ici aucun aléa à modéliser — l'omettre ou le minorer produirait un
/// solde prévisionnel flatteur et faux.
/// </remarks>
public sealed class SupplierEffetCollector : ICashFlowSourceCollector
{
    private readonly ITenantDbContextFactory _contextFactory;

    public SupplierEffetCollector(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public CashFlowSourceType SourceType => CashFlowSourceType.SupplierEffet;

    public bool IsEnabled(TreasuryForecastOptions options) => true;

    public async Task<IReadOnlyList<CashFlowLineDraft>> CollectAsync(
        CashFlowCollectionContext context,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = _contextFactory.CreateContext();

        var rows = await ctx.SupplierPayments
            .AsNoTracking()
            .Where(p => p.EffetStatus == EffetStatus.EnPortefeuille
                        && p.EffetDueDate != null
                        && p.EffetDueDate >= context.From
                        && p.EffetDueDate <= context.To)
            .Join(
                ctx.SupplierInvoices.AsNoTracking(),
                payment => payment.SupplierInvoiceId,
                invoice => invoice.Id,
                (payment, invoice) => new
                {
                    payment.Id,
                    DueDate = payment.EffetDueDate!.Value,
                    Amount = payment.Amount.Amount,
                    payment.Reference,
                    invoice.InvoiceNumber,
                    invoice.SupplierId
                })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return Array.Empty<CashFlowLineDraft>();

        var supplierIds = rows.Select(r => r.SupplierId).Distinct().ToList();
        var supplierNames = await ctx.Suppliers
            .AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        return rows
            .Where(r => r.Amount > 0m)
            .Select(r => new CashFlowLineDraft
            {
                Direction = CashFlowDirection.Outflow,
                SourceType = CashFlowSourceType.SupplierEffet,
                SourceId = r.Id,
                SourceReference = r.Reference ?? r.InvoiceNumber,
                Label = $"Traite fournisseur {r.Reference ?? r.InvoiceNumber}",
                ThirdPartyName = supplierNames.TryGetValue(r.SupplierId, out var name) ? name : null,
                ContractualDate = r.DueDate.Date,
                ExpectedDate = r.DueDate.Date,
                Amount = r.Amount,
                ProbabilityPercent = 100m,
                IsConfirmed = true
            })
            .ToList();
    }
}
