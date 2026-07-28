using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

/// <summary>
/// Vague 0 — correctif 4 : un avoir porte désormais un lien persistant vers la facture qu'il
/// rectifie. Auparavant ce lien ne vivait que dans le JSON des métadonnées du brouillon et
/// disparaissait à la soumission, laissant l'avoir sans référence structurée ni mention au PDF.
/// </summary>
public sealed class InvoiceLinkedInvoiceTests
{
    [Fact]
    public void CreateCreditNote_SetsLinkedInvoiceIdAndCreditNoteType()
    {
        var originalInvoiceId = Guid.NewGuid();

        var result = Invoice.CreateCreditNote(
            InvoiceNumber.Create("AVO", 2026, 3),
            NewClient(),
            new DateTime(2026, 7, 20),
            originalInvoiceId);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(originalInvoiceId, result.Value.LinkedInvoiceId);
        Assert.True(result.Value.IsCreditNote);
        Assert.Equal(InvoiceType.CreditNote, result.Value.Type);
    }

    [Fact]
    public void CreateCreditNote_WithoutOriginalInvoice_IsRejected()
    {
        var result = Invoice.CreateCreditNote(
            InvoiceNumber.Create("AVO", 2026, 4),
            NewClient(),
            new DateTime(2026, 7, 20),
            Guid.Empty);

        Assert.True(result.IsFailure);
        Assert.Contains("facture d'origine", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateCreditNote_PropagatesCreateValidation()
    {
        var result = Invoice.CreateCreditNote(
            InvoiceNumber.Create("AVO", 2026, 5),
            NewClient(),
            new DateTime(2026, 7, 20),
            Guid.NewGuid(),
            dueDate: new DateTime(2026, 7, 1));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void CreditNoteViaCreate_StaysAllowed_ForHistoricalCompatibility()
    {
        // L'obligation porte sur la fabrique dédiée, pas sur l'agrégat : les avoirs
        // historiques, chargés depuis la base sans LinkedInvoiceId, restent valides.
        var result = Invoice.Create(
            InvoiceNumber.Create("AVO", 2026, 6),
            NewClient(),
            new DateTime(2026, 7, 20),
            type: InvoiceType.CreditNote);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.True(result.Value.IsCreditNote);
        Assert.Null(result.Value.LinkedInvoiceId);
    }

    [Fact]
    public void StandardInvoice_HasNoLinkedInvoiceId()
    {
        var result = Invoice.Create(
            InvoiceNumber.Create("FAC", 2026, 7),
            NewClient(),
            new DateTime(2026, 7, 20));

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Null(result.Value.LinkedInvoiceId);
        Assert.Null(result.Value.SourceDeliveryNoteId);
        Assert.Null(result.Value.SourceQuoteId);
    }

    [Fact]
    public void CreditNote_KeepsLinkThroughLifecycle()
    {
        var originalInvoiceId = Guid.NewGuid();
        var creditNote = Invoice.CreateCreditNote(
            InvoiceNumber.Create("AVO", 2026, 8),
            NewClient(),
            new DateTime(2026, 7, 20),
            originalInvoiceId).Value;

        Assert.True(creditNote.AddCustomLine(
            "Retour marchandise", null, 1m, "Unité", Money.Create(250m), VatRate.Standard).IsSuccess);
        Assert.True(creditNote.Validate().IsSuccess);

        Assert.Equal(originalInvoiceId, creditNote.LinkedInvoiceId);
        Assert.Equal(-250m, creditNote.SubTotal.Amount);
    }

    private static Client NewClient()
    {
        var address = Address.Create("1 rue de la République", "Tunis", "Tunis").Value;
        var email = Email.Create("client@example.com").Value;
        return Client.Create("Client test", ClientType.Individual, address, email).Value;
    }
}
