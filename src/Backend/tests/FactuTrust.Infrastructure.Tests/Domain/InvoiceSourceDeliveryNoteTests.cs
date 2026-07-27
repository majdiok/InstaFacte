using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Vague 0 — correctif 1 : la fabrique <see cref="Invoice.CreateFromDeliveryNote"/> établit le
/// lien permanent BL → facture, seule source de vérité du garde-fou anti-double-déduction.
/// </summary>
public sealed class InvoiceSourceDeliveryNoteTests
{
    [Fact]
    public void CreateFromDeliveryNote_SetsSourceDeliveryNoteId()
    {
        var deliveryNoteId = Guid.NewGuid();

        var result = Invoice.CreateFromDeliveryNote(
            InvoiceNumber.Create("FAC", 2026, 7),
            NewClient(),
            new DateTime(2026, 7, 20),
            deliveryNoteId,
            reference: "Référence libre sans rapport");

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(deliveryNoteId, result.Value.SourceDeliveryNoteId);
        Assert.Null(result.Value.SourceQuoteId);
    }

    [Fact]
    public void CreateFromDeliveryNote_WithEmptyId_IsRejected()
    {
        var result = Invoice.CreateFromDeliveryNote(
            InvoiceNumber.Create("FAC", 2026, 8),
            NewClient(),
            new DateTime(2026, 7, 20),
            Guid.Empty);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void CreateFromDeliveryNote_PropagatesCreateValidation()
    {
        // Échéance antérieure à l'émission : la règle de Create doit rester appliquée.
        var result = Invoice.CreateFromDeliveryNote(
            InvoiceNumber.Create("FAC", 2026, 9),
            NewClient(),
            new DateTime(2026, 7, 20),
            Guid.NewGuid(),
            dueDate: new DateTime(2026, 7, 1));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_LeavesSourceDeliveryNoteIdNull()
    {
        var result = Invoice.Create(
            InvoiceNumber.Create("FAC", 2026, 10),
            NewClient(),
            new DateTime(2026, 7, 20),
            reference: "BL du 12/07");

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Null(result.Value.SourceDeliveryNoteId);
    }

    private static Client NewClient()
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        return Client.Create("Client test", ClientType.Individual, address, email).Value;
    }
}
