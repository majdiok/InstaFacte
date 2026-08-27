using FactuTrust.Application.Features.Accounting.Services;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class CashOperationJournalLabelBuilderTests
{
    [Fact]
    public void BuildJournalLabel_CreditClientReceivables_IncludesExpectedSegments()
    {
        var number = CashOperationNumber.Create(CashOperationNumber.CreditPrefix, 2026, 3).Value;
        var op = CashOperation.Create(
            number,
            CashOperationType.Credit,
            DateTime.UtcNow.Date,
            PaymentMethod.Cash,
            Money.Create(500m, Money.DefaultCurrency),
            label: "client 5556",
            revenueCategory: CashRevenueCategory.ClientReceivablesReceipt).Value;

        var label = CashOperationJournalLabelBuilder.BuildJournalLabel(op);

        Assert.Contains("Encaissement", label);
        Assert.Contains("Espèces", label);
        Assert.Contains("Encaissement créances clients", label);
        Assert.Contains("client 5556", label);
        Assert.Contains("ENC-2026-000003", label);
        Assert.True(label.Length <= CashOperationJournalLabelBuilder.MaxJournalLabelLength);
    }

    [Fact]
    public void BuildJournalLabel_DebitExpense_IncludesDecaissementAndMethod()
    {
        var number = CashOperationNumber.Create(CashOperationNumber.DebitPrefix, 2026, 12).Value;
        var op = CashOperation.Create(
            number,
            CashOperationType.Debit,
            DateTime.UtcNow.Date,
            PaymentMethod.BankTransfer,
            Money.Create(100m, Money.DefaultCurrency),
            label: "Fournisseur X",
            category: CashExpenseCategory.SuppliesAndConsumables).Value;

        var label = CashOperationJournalLabelBuilder.BuildJournalLabel(op);

        Assert.Contains("Décaissement", label);
        Assert.Contains("Virement bancaire", label);
        Assert.Contains("fournitures", label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Fournisseur X", label);
        Assert.Contains("DEP-2026-000012", label);
        Assert.True(label.Length <= CashOperationJournalLabelBuilder.MaxJournalLabelLength);
    }

    [Fact]
    public void BuildJournalLabel_WithReference_AppendsReferenceSegment()
    {
        var number = CashOperationNumber.Create(CashOperationNumber.CreditPrefix, 2026, 1).Value;
        var op = CashOperation.Create(
            number,
            CashOperationType.Credit,
            DateTime.UtcNow.Date,
            PaymentMethod.Check,
            Money.Create(50m, Money.DefaultCurrency),
            label: "Paiement",
            revenueCategory: CashRevenueCategory.CashSalesReceipt,
            reference: "CHQ 12345").Value;

        var label = CashOperationJournalLabelBuilder.BuildJournalLabel(op);

        Assert.Contains("Réf. CHQ 12345", label);
        Assert.True(label.Length <= CashOperationJournalLabelBuilder.MaxJournalLabelLength);
    }

    [Fact]
    public void BuildJournalLabel_VeryLongUserLabel_StaysWithinMaxLengthAndKeepsDocumentNumber()
    {
        var longLabel = new string('x', 480);
        var number = CashOperationNumber.Create(CashOperationNumber.CreditPrefix, 2026, 99).Value;
        var op = CashOperation.Create(
            number,
            CashOperationType.Credit,
            DateTime.UtcNow.Date,
            PaymentMethod.Cash,
            Money.Create(1m, Money.DefaultCurrency),
            label: longLabel,
            revenueCategory: CashRevenueCategory.Other).Value;

        var label = CashOperationJournalLabelBuilder.BuildJournalLabel(op);

        Assert.True(label.Length <= CashOperationJournalLabelBuilder.MaxJournalLabelLength);
        Assert.Contains("ENC-2026-000099", label);
    }

    [Fact]
    public void BuildLineLabel_AppendsSuffix()
    {
        var result = CashOperationJournalLabelBuilder.BuildLineLabel("Encaissement · Espèces · doc", " — HT");
        Assert.Equal("Encaissement · Espèces · doc — HT", result);
    }

    [Fact]
    public void BuildLineLabel_EmptySuffix_ReturnsBaseUnchanged()
    {
        var result = CashOperationJournalLabelBuilder.BuildLineLabel("Encaissement · Espèces · doc", string.Empty);
        Assert.Equal("Encaissement · Espèces · doc", result);
    }

    [Fact]
    public void BuildLineLabel_LongBase_TruncatesBaseButPreservesSuffix()
    {
        var longBase = new string('x', 495);
        var suffix = " — TVA 19%";

        var result = CashOperationJournalLabelBuilder.BuildLineLabel(longBase, suffix);

        Assert.True(result.Length <= CashOperationJournalLabelBuilder.MaxJournalLabelLength);
        Assert.EndsWith(suffix, result);
        Assert.True(result.Length == CashOperationJournalLabelBuilder.MaxJournalLabelLength);
    }

    [Fact]
    public void BuildLineLabel_SuffixLongerThanMax_TruncatesSuffixAsLastResort()
    {
        var suffix = new string('y', CashOperationJournalLabelBuilder.MaxJournalLabelLength + 10);

        var result = CashOperationJournalLabelBuilder.BuildLineLabel("base", suffix);

        Assert.Equal(CashOperationJournalLabelBuilder.MaxJournalLabelLength, result.Length);
    }
}
