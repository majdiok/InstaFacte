using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Invariants de domaine des effets de commerce (traites) portés par Payment / SupplierPayment :
/// échéance obligatoire, statut initial « en portefeuille », et transitions de règlement.
/// </summary>
public sealed class EffetDeCommerceDomainTests
{
    private static Invoice NewInvoice()
    {
        var address = Address.Create("1 rue de la Republique", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        var client = Client.Create("Client test", ClientType.Individual, address, email).Value;
        return Invoice.Create(
            InvoiceNumber.Create("FAC", 2026, 42),
            client,
            issueDate: new DateTime(2026, 4, 20),
            dueDate: new DateTime(2026, 4, 20)).Value;
    }

    private static Payment Traite(DateTime? due) =>
        Payment.Create(NewInvoice(), Money.Create(100m, Money.DefaultCurrency), new DateTime(2026, 4, 20),
            PaymentMethod.Traite, effetDueDate: due).Value;

    [Fact]
    public void Payment_Traite_WithoutDueDate_Fails()
    {
        var result = Payment.Create(NewInvoice(), Money.Create(100m, Money.DefaultCurrency),
            new DateTime(2026, 4, 20), PaymentMethod.Traite, effetDueDate: null);

        Assert.True(result.IsFailure);
        Assert.Contains("échéance", result.Error.Description);
    }

    [Fact]
    public void Payment_Traite_WithDueDate_StartsEnPortefeuille()
    {
        var payment = Traite(new DateTime(2026, 6, 20));

        Assert.True(payment.IsEffet);
        Assert.Equal(EffetStatus.EnPortefeuille, payment.EffetStatus);
        Assert.Equal(new DateTime(2026, 6, 20), payment.EffetDueDate);
        Assert.Null(payment.EffetSettledAt);
    }

    [Fact]
    public void Payment_NonTraite_HasNoEffetState()
    {
        var payment = Payment.Create(NewInvoice(), Money.Create(100m, Money.DefaultCurrency),
            new DateTime(2026, 4, 20), PaymentMethod.BankTransfer).Value;

        Assert.False(payment.IsEffet);
        Assert.Null(payment.EffetStatus);
        Assert.Null(payment.EffetDueDate);
    }

    [Fact]
    public void MarkEffetSettled_OnNonTraite_Fails()
    {
        var payment = Payment.Create(NewInvoice(), Money.Create(100m, Money.DefaultCurrency),
            new DateTime(2026, 4, 20), PaymentMethod.Cash).Value;

        var result = payment.MarkEffetSettled(new DateTime(2026, 6, 20), EffetStatus.Encaisse);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void MarkEffetSettled_EnPortefeuille_SetsStatusAndDate()
    {
        var payment = Traite(new DateTime(2026, 6, 20));

        var result = payment.MarkEffetSettled(new DateTime(2026, 6, 25), EffetStatus.Encaisse);

        Assert.True(result.IsSuccess);
        Assert.Equal(EffetStatus.Encaisse, payment.EffetStatus);
        Assert.Equal(new DateTime(2026, 6, 25), payment.EffetSettledAt);
    }

    [Fact]
    public void MarkEffetSettled_Twice_SecondCallFails()
    {
        var payment = Traite(new DateTime(2026, 6, 20));
        payment.MarkEffetSettled(new DateTime(2026, 6, 25), EffetStatus.Encaisse);

        var second = payment.MarkEffetSettled(new DateTime(2026, 6, 26), EffetStatus.Impaye);

        Assert.True(second.IsFailure);
        Assert.Equal(EffetStatus.Encaisse, payment.EffetStatus);
    }
}
