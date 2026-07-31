using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Classement d'un lot de factures dans les tranches d'ancienneté du recouvrement.
///
/// <b>Conventions.</b>
///  - Le solde ventilé est le <b>reste dû</b> (TTC − encaissé), pas le total facturé : une
///    facture soldée sort du calcul, sinon la balance âgée gonflerait à mesure des ventes.
///  - Un trop-perçu ne vient PAS en déduction des autres tranches : cela relève du lettrage.
///    Le neutraliser à la ligne évite de minorer une tranche à tort.
///  - Une facture <b>sans échéance</b> est traitée comme <see cref="NotDue"/> : sans date, on
///    ne peut pas la déclarer exigible, et l'inventer par défaut créerait des retards fictifs.
///  - Les avoirs (<see cref="Domain.Enums.InvoiceType.CreditNote"/>) sortent aussi du calcul :
///    ils sont une déduction, pas une créance datée.
///
/// <b>Bornes.</b> Tranche « 1-30 jours d'échéance dépassée » ⇒ 1 ≤ retard ≤ 30. Le jour même
/// (retard = 0) n'est pas encore en retard, il compte donc dans <see cref="NotDue"/>. C'est la
/// convention comptable la plus courante en France et en Tunisie.
/// </summary>
public readonly record struct AgingBuckets(
    decimal NotDue,
    decimal B0To30,
    decimal B31To60,
    decimal B61To90,
    decimal BOver90)
{
    public static AgingBuckets Empty => new(0m, 0m, 0m, 0m, 0m);

    public static AgingBuckets Compute(
        IEnumerable<Invoice> invoices,
        IReadOnlyDictionary<Guid, decimal> paidByInvoice,
        DateTime asOfDate)
    {
        decimal notDue = 0, b0to30 = 0, b31to60 = 0, b61to90 = 0, bOver90 = 0;
        var day = asOfDate.Date;

        foreach (var invoice in invoices)
        {
            if (invoice.Type == Domain.Enums.InvoiceType.CreditNote)
                continue;

            var paid = paidByInvoice.TryGetValue(invoice.Id, out var p) ? p : 0m;
            var remaining = invoice.TotalAmount.Amount - paid;
            if (remaining <= 0) continue;

            if (invoice.DueDate is not { } due)
            {
                notDue += remaining;
                continue;
            }

            var overdueDays = (day - due.Date).Days;

            if (overdueDays <= 0) notDue += remaining;
            else if (overdueDays <= 30) b0to30 += remaining;
            else if (overdueDays <= 60) b31to60 += remaining;
            else if (overdueDays <= 90) b61to90 += remaining;
            else bOver90 += remaining;
        }

        return new AgingBuckets(notDue, b0to30, b31to60, b61to90, bOver90);
    }
}
