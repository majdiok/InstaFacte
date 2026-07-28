using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Features.Invoices.Services;

/// <summary>
/// Résolution du montant d'un encaissement client : restant dû, net reçu, total imputé.
///
/// Extrait de <c>RecordInvoicePaymentCommandHandler</c> pour être partagé à l'identique avec
/// l'encaissement fractionné (N règlements atomiques). Aucune règle n'a changé à l'extraction :
/// c'est la même logique, appelée depuis deux points.
/// </summary>
public static class InvoicePaymentCalculator
{
    /// <summary>Un encaissement résolu : ce qui entre en caisse, et ce qui solde la facture.</summary>
    public readonly record struct ResolvedPayment(decimal NetReceived, decimal AppliedTowardInvoice);

    /// <summary>
    /// Calcule le montant à encaisser à partir du restant dû.
    /// </summary>
    /// <param name="invoice">Facture (ou avoir) concernée.</param>
    /// <param name="alreadyApplied">
    /// Total déjà imputé sur la facture — encaissements antérieurs, plus, en mode fractionné,
    /// les règlements du même lot déjà résolus.
    /// </param>
    /// <param name="requestedAmount">Montant net demandé. Null = solder le restant dû.</param>
    /// <param name="withholding">Retenue à la source subie sur cet encaissement.</param>
    public static Result<ResolvedPayment> Resolve(
        Invoice invoice,
        decimal alreadyApplied,
        decimal? requestedAmount,
        decimal withholding)
    {
        if (withholding < 0)
            return Result.Failure<ResolvedPayment>(
                Error.Validation("ClientWithholdingAmount", "La retenue subie ne peut pas être négative"));

        // Comparaison sur les magnitudes : pour un avoir, TotalAmount est négatif alors que le
        // remboursement est enregistré comme un scalaire positif de sens opposé.
        var remainingAmount = Math.Abs(invoice.TotalAmount.Amount) - alreadyApplied;

        decimal netReceived;
        if (requestedAmount.HasValue)
            netReceived = requestedAmount.Value;
        else if (withholding > 0)
            netReceived = remainingAmount - withholding;
        else
            netReceived = remainingAmount;

        if (netReceived <= 0)
            return Result.Failure<ResolvedPayment>(
                Error.Validation("Amount", "Le montant du paiement doit être positif"));

        var appliedTowardInvoice = netReceived + withholding;
        if (appliedTowardInvoice > remainingAmount)
            return Result.Failure<ResolvedPayment>(Error.Validation("Amount",
                $"Le total (net + retenue subie) ne peut pas dépasser le restant dû ({remainingAmount:N3} {invoice.TotalAmount.Currency})"));

        return Result.Success(new ResolvedPayment(netReceived, appliedTowardInvoice));
    }
}
