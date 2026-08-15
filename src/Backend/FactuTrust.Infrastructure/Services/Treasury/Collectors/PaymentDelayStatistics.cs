using FactuTrust.Application.Common.Interfaces.Treasury;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.Treasury.Collectors;

/// <summary>
/// Retard de paiement observé, par client et pour la société, mesuré sur les factures réellement
/// soldées.
/// </summary>
/// <remarks>
/// <para>
/// C'est la seule partie « apprise » du moteur, et la plus utile : un client qui règle
/// systématiquement à 45 jours sur une échéance à 30 doit être projeté à 45. La médiane est
/// préférée à la moyenne parce qu'un unique litige ancien déplacerait toute la projection.
/// </para>
/// <para>
/// Le retard d'un client n'est retenu qu'au-delà d'un nombre minimal de factures soldées
/// (<c>MinInvoicesForClientDelayMedian</c>) ; en deçà, la médiane société s'applique. Le décalage
/// est enfin plafonné par <c>MaxPaymentDelayShiftDays</c>.
/// </para>
/// </remarks>
public sealed class PaymentDelayStatistics
{
    private readonly IReadOnlyDictionary<Guid, int> _byClient;
    private readonly int _companyMedian;
    private readonly int _maxShiftDays;

    private PaymentDelayStatistics(
        IReadOnlyDictionary<Guid, int> byClient,
        int companyMedian,
        int maxShiftDays)
    {
        _byClient = byClient;
        _companyMedian = companyMedian;
        _maxShiftDays = maxShiftDays;
    }

    /// <summary>Statistiques vides : aucun décalage appliqué (utile en test et sans historique).</summary>
    public static PaymentDelayStatistics Empty { get; } =
        new(new Dictionary<Guid, int>(), 0, 0);

    /// <summary>Décalage en jours à appliquer à l'échéance d'une facture de ce client.</summary>
    public int ResolveShiftDays(Guid clientId)
    {
        var days = _byClient.TryGetValue(clientId, out var clientMedian) ? clientMedian : _companyMedian;
        return Math.Clamp(days, 0, _maxShiftDays);
    }

    /// <summary>Retard médian de la société, exposé pour la narration et les tests.</summary>
    public int CompanyMedianDelayDays => _companyMedian;

    public static async Task<PaymentDelayStatistics> LoadAsync(
        TenantDbContext ctx,
        CashFlowCollectionContext context,
        CancellationToken cancellationToken)
    {
        var since = context.Today.AddMonths(-Math.Max(1, context.Options.HistoryMonthsForVolatility));

        // Factures soldées de la période, avec la date du dernier règlement effectif.
        var settled = await ctx.Invoices
            .AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Paid
                        && i.Type != InvoiceType.CreditNote
                        && i.CancelledAt == null
                        && i.DueDate != null
                        && i.PaidAt != null
                        && i.PaidAt >= since)
            .Select(i => new
            {
                i.ClientId,
                DueDate = i.DueDate!.Value,
                PaidAt = i.PaidAt!.Value
            })
            .ToListAsync(cancellationToken);

        if (settled.Count == 0)
            return new PaymentDelayStatistics(
                new Dictionary<Guid, int>(),
                0,
                context.Options.MaxPaymentDelayShiftDays);

        // Un règlement en avance ne raccourcit pas la projection : anticiper un encaissement est
        // le seul type d'erreur qui puisse faire prendre une décision de trésorerie à tort.
        var delays = settled
            .Select(s => new
            {
                s.ClientId,
                Days = Math.Max(0, (int)(s.PaidAt.Date - s.DueDate.Date).TotalDays)
            })
            .ToList();

        var companyMedian = Median(delays.Select(d => d.Days));

        var minInvoices = Math.Max(1, context.Options.MinInvoicesForClientDelayMedian);
        var byClient = delays
            .GroupBy(d => d.ClientId)
            .Where(g => g.Count() >= minInvoices)
            .ToDictionary(g => g.Key, g => Median(g.Select(d => d.Days)));

        return new PaymentDelayStatistics(
            byClient,
            companyMedian,
            context.Options.MaxPaymentDelayShiftDays);
    }

    internal static int Median(IEnumerable<int> values)
    {
        var ordered = values.OrderBy(v => v).ToList();
        if (ordered.Count == 0) return 0;

        var mid = ordered.Count / 2;
        return ordered.Count % 2 == 1
            ? ordered[mid]
            : (int)Math.Round((ordered[mid - 1] + ordered[mid]) / 2.0, MidpointRounding.AwayFromZero);
    }
}
