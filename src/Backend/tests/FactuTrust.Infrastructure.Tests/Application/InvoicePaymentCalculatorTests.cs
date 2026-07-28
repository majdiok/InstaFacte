using FactuTrust.Application.Features.Invoices.Services;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Vague 0 — correctif 5 : le calcul du montant d'un encaissement, extrait de
/// RecordInvoicePaymentCommandHandler pour être partagé à l'identique avec l'encaissement
/// fractionné. Ces tests figent la règle des deux côtés, et vérifient qu'un encaissement
/// en plusieurs modes se comporte exactement comme des encaissements successifs.
/// </summary>
public sealed class InvoicePaymentCalculatorTests
{
    [Fact]
    public void WithoutRequestedAmount_SettlesTheWholeRemainder()
    {
        var invoice = NewInvoice(totalHt: 1000m); // 1000 HT + 190 TVA = 1190 TTC

        var result = InvoicePaymentCalculator.Resolve(invoice, alreadyApplied: 0m, requestedAmount: null, withholding: 0m);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(1190m, result.Value.NetReceived);
        Assert.Equal(1190m, result.Value.AppliedTowardInvoice);
    }

    [Fact]
    public void WithholdingReducesNetReceived_ButStillSettlesTheInvoice()
    {
        var invoice = NewInvoice(totalHt: 1000m);

        var result = InvoicePaymentCalculator.Resolve(invoice, alreadyApplied: 0m, requestedAmount: null, withholding: 15m);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(1175m, result.Value.NetReceived);          // encaissé
        Assert.Equal(1190m, result.Value.AppliedTowardInvoice); // imputé sur la facture
    }

    [Fact]
    public void SplitPayments_BehaveLikeSuccessiveSettlements()
    {
        // Cœur du correctif : trois modes de règlement sur une même facture. Le restant dû
        // décroît règlement après règlement, exactement comme trois appels unitaires.
        var invoice = NewInvoice(totalHt: 1000m); // 1190 TTC
        var applied = 0m;

        foreach (var amount in new[] { 500m, 400m, 290m })
        {
            var step = InvoicePaymentCalculator.Resolve(invoice, applied, amount, withholding: 0m);
            Assert.True(step.IsSuccess, step.Error?.Description);
            applied += step.Value.AppliedTowardInvoice;
        }

        Assert.Equal(1190m, applied);
    }

    [Fact]
    public void SplitPayments_LastOneCanSettleTheRemainderImplicitly()
    {
        var invoice = NewInvoice(totalHt: 1000m);

        var first = InvoicePaymentCalculator.Resolve(invoice, 0m, 700m, 0m);
        Assert.True(first.IsSuccess);

        var last = InvoicePaymentCalculator.Resolve(invoice, first.Value.AppliedTowardInvoice, null, 0m);
        Assert.True(last.IsSuccess, last.Error?.Description);
        Assert.Equal(490m, last.Value.NetReceived);
    }

    [Fact]
    public void Overpayment_IsRejected()
    {
        var invoice = NewInvoice(totalHt: 1000m);

        var result = InvoicePaymentCalculator.Resolve(invoice, alreadyApplied: 0m, requestedAmount: 1200m, withholding: 0m);

        Assert.True(result.IsFailure);
        Assert.Contains("restant dû", result.Error.Description);
    }

    [Fact]
    public void SplitPayments_ExceedingTheTotal_AreRejectedOnTheOffendingLine()
    {
        var invoice = NewInvoice(totalHt: 1000m);

        var first = InvoicePaymentCalculator.Resolve(invoice, 0m, 1000m, 0m);
        Assert.True(first.IsSuccess);

        // 1000 + 300 > 1190 : le second règlement doit être refusé, ce qui fait échouer
        // l'ensemble du lot côté handler (transaction unique).
        var second = InvoicePaymentCalculator.Resolve(invoice, first.Value.AppliedTowardInvoice, 300m, 0m);
        Assert.True(second.IsFailure);
    }

    [Fact]
    public void NegativeWithholding_IsRejected()
    {
        var invoice = NewInvoice(totalHt: 1000m);

        var result = InvoicePaymentCalculator.Resolve(invoice, 0m, null, withholding: -5m);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void FullySettledInvoice_RefusesFurtherPayment()
    {
        var invoice = NewInvoice(totalHt: 1000m);

        var result = InvoicePaymentCalculator.Resolve(invoice, alreadyApplied: 1190m, requestedAmount: null, withholding: 0m);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void CreditNote_ComparesOnMagnitudes()
    {
        // Un avoir porte un TTC négatif, mais le remboursement est un scalaire positif.
        var creditNote = NewInvoice(totalHt: 500m, InvoiceType.CreditNote);
        Assert.True(creditNote.TotalAmount.Amount < 0);

        var result = InvoicePaymentCalculator.Resolve(creditNote, 0m, null, 0m);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(595m, result.Value.NetReceived);
    }

    private static Invoice NewInvoice(decimal totalHt, InvoiceType type = InvoiceType.Standard)
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;
        var prefix = type == InvoiceType.CreditNote ? "AVO" : "FAC";

        var invoice = Invoice.Create(
            InvoiceNumber.Create(prefix, 2026, 31),
            client,
            new DateTime(2026, 7, 20),
            type: type).Value;

        Assert.True(invoice.AddCustomLine(
            "Article", null, 1m, "Unité", Money.Create(totalHt), VatRate.Standard).IsSuccess);

        return invoice;
    }
}
