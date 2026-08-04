using FactuTrust.Domain.Entities.Honoraires;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Honoraires;

public sealed class HonorairesInvoiceTests
{
    [Fact]
    public void CreditNote_TotalsAreNegative()
    {
        var invoice = HonorairesInvoice.CreateCreditNoteDraft(
            Guid.NewGuid(), "Client SARL", Guid.NewGuid(), DateTime.UtcNow).Value;

        var add = invoice.AddLine("Mission", null, 1m, Money.Create(1000m), VatRate.Standard);
        Assert.True(add.IsSuccess);
        Assert.Equal(-1000m, invoice.SubTotal.Amount);
        Assert.Equal(-190m, invoice.TotalVat.Amount);
        Assert.Equal(-1190m, invoice.TotalAmount.Amount);
    }

    [Fact]
    public void Validate_RequiresNumberAndLines()
    {
        var invoice = HonorairesInvoice.CreateDraft(
            Guid.NewGuid(), "Client SARL", DateTime.UtcNow).Value;

        Assert.True(invoice.Validate().IsFailure);

        invoice.AddLine("Conseil", null, 1m, Money.Create(100m), VatRate.Standard);
        Assert.True(invoice.Validate().IsFailure);

        invoice.AssignNumber("FAC-2026-001");
        Assert.True(invoice.Validate().IsSuccess);
        Assert.Equal(HonorairesInvoiceStatus.Validated, invoice.Status);
    }

    [Fact]
    public void AddLine_WithActivityCode_StoresNormalizedCode()
    {
        var invoice = HonorairesInvoice.CreateDraft(
            Guid.NewGuid(), "Client SARL", DateTime.UtcNow).Value;

        var add = invoice.AddLine(
            "Tenue comptable / saisie",
            "Janvier 2026",
            1m,
            Money.Create(700m),
            VatRate.Standard,
            discountPercent: null,
            activityCode: "tenue");

        Assert.True(add.IsSuccess);
        var line = Assert.Single(invoice.Lines);
        Assert.Equal("TENUE", line.ActivityCode);
        Assert.Equal("Tenue comptable / saisie", line.Designation);
        Assert.Equal("Janvier 2026", line.Description);
    }

    [Fact]
    public void ReplaceLines_PreservesActivityCodes()
    {
        var invoice = HonorairesInvoice.CreateDraft(
            Guid.NewGuid(), "Client SARL", DateTime.UtcNow).Value;

        var replace = invoice.ReplaceLines(new[]
        {
            ((string?)null, "Mission libre", (string?)null, 1m, Money.Create(100m), VatRate.Standard, (decimal?)null),
            ("FISC-M", "Déclarations mensuelles (TVA, RS, TCL, TFP/FOPROLOS)", "Mars 2026", 1m, Money.Create(200m), VatRate.Standard, (decimal?)null)
        });

        Assert.True(replace.IsSuccess);
        Assert.Equal(2, invoice.Lines.Count);
        Assert.Null(invoice.Lines.ElementAt(0).ActivityCode);
        Assert.Equal("FISC-M", invoice.Lines.ElementAt(1).ActivityCode);
        Assert.Equal("Mars 2026", invoice.Lines.ElementAt(1).Description);
    }

    [Fact]
    public void RecordPayment_PartialThenFull_UpdatesStatus()
    {
        var invoice = HonorairesInvoice.CreateDraft(
            Guid.NewGuid(), "Client SARL", DateTime.UtcNow).Value;
        invoice.AddLine("Mission", null, 1m, Money.Create(1000m), VatRate.Exempt);
        invoice.AssignNumber("FAC-2026-002");
        invoice.Validate();

        var p1 = HonorairesPayment.Create(
            invoice, DateTime.UtcNow, Money.Create(400m), Money.Zero(), PaymentMethod.BankTransfer).Value;
        Assert.True(invoice.RecordPayment(p1).IsSuccess);
        Assert.Equal(HonorairesInvoiceStatus.PartiallyPaid, invoice.Status);

        var p2 = HonorairesPayment.Create(
            invoice, DateTime.UtcNow, Money.Create(500m), Money.Create(100m), PaymentMethod.BankTransfer).Value;
        Assert.True(invoice.RecordPayment(p2).IsSuccess);
        Assert.Equal(HonorairesInvoiceStatus.Paid, invoice.Status);
    }

    [Fact]
    public void RecordPayment_Overpay_Fails()
    {
        var invoice = HonorairesInvoice.CreateDraft(
            Guid.NewGuid(), "Client SARL", DateTime.UtcNow).Value;
        invoice.AddLine("Mission", null, 1m, Money.Create(100m), VatRate.Exempt);
        invoice.AssignNumber("FAC-2026-003");
        invoice.Validate();

        var payment = HonorairesPayment.Create(
            invoice, DateTime.UtcNow, Money.Create(150m), Money.Zero(), PaymentMethod.Cash).Value;
        Assert.True(invoice.RecordPayment(payment).IsFailure);
    }

    [Theory]
    [InlineData(HonorairesInvoiceStatus.Draft)]
    [InlineData(HonorairesInvoiceStatus.Paid)]
    [InlineData(HonorairesInvoiceStatus.Cancelled)]
    public void RecordPayment_Rejected_WhenStatusNotPayable(HonorairesInvoiceStatus status)
    {
        var invoice = HonorairesInvoice.CreateDraft(
            Guid.NewGuid(), "Client SARL", DateTime.UtcNow).Value;
        invoice.AddLine("Mission", null, 1m, Money.Create(100m), VatRate.Exempt);
        invoice.AssignNumber("FAC-2026-004");
        invoice.Validate();

        if (status == HonorairesInvoiceStatus.Paid)
        {
            var full = HonorairesPayment.Create(
                invoice, DateTime.UtcNow, Money.Create(100m), Money.Zero(), PaymentMethod.Cash).Value;
            Assert.True(invoice.RecordPayment(full).IsSuccess);
            Assert.Equal(HonorairesInvoiceStatus.Paid, invoice.Status);
        }
        else if (status == HonorairesInvoiceStatus.Cancelled)
        {
            Assert.True(invoice.Cancel().IsSuccess);
        }
        else
        {
            // Draft: rebuild without validate
            invoice = HonorairesInvoice.CreateDraft(
                Guid.NewGuid(), "Client SARL", DateTime.UtcNow).Value;
            invoice.AddLine("Mission", null, 1m, Money.Create(100m), VatRate.Exempt);
            invoice.AssignNumber("FAC-2026-005");
        }

        var payment = HonorairesPayment.Create(
            invoice, DateTime.UtcNow, Money.Create(10m), Money.Zero(), PaymentMethod.Cash).Value;
        Assert.True(invoice.RecordPayment(payment).IsFailure);
    }

    [Fact]
    public void RecordPayment_OnCreditNote_Fails()
    {
        var credit = HonorairesInvoice.CreateCreditNoteDraft(
            Guid.NewGuid(), "Client SARL", Guid.NewGuid(), DateTime.UtcNow).Value;
        credit.AddLine("Mission", null, 1m, Money.Create(100m), VatRate.Exempt);
        credit.AssignNumber("AVO-2026-001");
        credit.Validate();

        var payment = HonorairesPayment.Create(
            credit, DateTime.UtcNow, Money.Create(50m), Money.Zero(), PaymentMethod.BankTransfer).Value;
        Assert.True(credit.RecordPayment(payment).IsFailure);
    }

    [Fact]
    public void CanBePaymentRecorded_OnlyValidatedAndPartiallyPaid()
    {
        Assert.False(HonorairesInvoiceStatus.Draft.CanBePaymentRecorded());
        Assert.True(HonorairesInvoiceStatus.Validated.CanBePaymentRecorded());
        Assert.False(HonorairesInvoiceStatus.Paid.CanBePaymentRecorded());
        Assert.True(HonorairesInvoiceStatus.PartiallyPaid.CanBePaymentRecorded());
        Assert.False(HonorairesInvoiceStatus.Cancelled.CanBePaymentRecorded());
    }
}
