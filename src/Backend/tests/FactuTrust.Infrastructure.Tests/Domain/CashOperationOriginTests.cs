using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class CashOperationOriginTests
{
    [Fact]
    public void CreateFromInvoicePayment_ShouldForceInvoiceOriginCreditAndSource()
    {
        var number = CashOperationNumber.Create(CashOperationNumber.CreditPrefix, 2026, 11).Value;
        var amount = Money.Create(813.450m, Money.DefaultCurrency);
        var paymentId = Guid.NewGuid();

        var result = CashOperation.CreateFromInvoicePayment(
            number: number,
            paymentId: paymentId,
            invoiceNumber: "FAC-2026-000089",
            amount: amount,
            paymentDate: new DateTime(2026, 4, 20),
            reference: "POS-0001");

        Assert.True(result.IsSuccess, result.Error?.Description);
        var operation = result.Value;

        Assert.Equal(CashOperationType.Credit, operation.OperationType);
        Assert.Equal(PaymentMethod.Cash, operation.Method);
        Assert.Equal(CashOperationOrigin.InvoicePayment, operation.Origin);
        Assert.Equal("Payment", operation.SourceType);
        Assert.Equal(paymentId, operation.SourceId);
        Assert.Equal("Encaissement facture FAC-2026-000089", operation.Label);
        Assert.False(operation.CanBeCancelledDirectly());
    }

    [Fact]
    public void Cancel_ShouldFailForInvoicePaymentOrigin()
    {
        var number = CashOperationNumber.Create(CashOperationNumber.CreditPrefix, 2026, 12).Value;
        var amount = Money.Create(100m, Money.DefaultCurrency);

        var result = CashOperation.CreateFromInvoicePayment(
            number: number,
            paymentId: Guid.NewGuid(),
            invoiceNumber: "FAC-2026-000090",
            amount: amount,
            paymentDate: DateTime.UtcNow.Date);

        Assert.True(result.IsSuccess, result.Error?.Description);
        var cancelResult = result.Value.Cancel("Test");

        Assert.True(cancelResult.IsFailure);
        Assert.Equal("Validation.Origin", cancelResult.Error.Code);
    }

    [Fact]
    public void CreateFromSupplierPayment_ShouldForceSupplierOriginDebitAndSource()
    {
        var number = CashOperationNumber.Create(CashOperationNumber.DebitPrefix, 2026, 21).Value;
        var amount = Money.Create(2571.400m, Money.DefaultCurrency);
        var paymentId = Guid.NewGuid();

        var result = CashOperation.CreateFromSupplierPayment(
            number: number,
            supplierPaymentId: paymentId,
            supplierInvoiceNumber: "FS-2026-JPX4U",
            amount: amount,
            paymentDate: new DateTime(2026, 4, 21),
            reference: "REF-1");

        Assert.True(result.IsSuccess, result.Error?.Description);
        var operation = result.Value;

        Assert.Equal(CashOperationType.Debit, operation.OperationType);
        Assert.Equal(PaymentMethod.Cash, operation.Method);
        Assert.Equal(CashOperationOrigin.SupplierPayment, operation.Origin);
        Assert.Equal("SupplierPayment", operation.SourceType);
        Assert.Equal(paymentId, operation.SourceId);
        Assert.Equal("Paiement facture fournisseur FS-2026-JPX4U", operation.Label);
        Assert.Equal(CashExpenseCategory.SupplierInvoicePayment, operation.Category);
        Assert.False(operation.CanBeCancelledDirectly());
    }

    [Fact]
    public void Cancel_ShouldFailForSupplierPaymentOrigin()
    {
        var number = CashOperationNumber.Create(CashOperationNumber.DebitPrefix, 2026, 22).Value;
        var amount = Money.Create(100m, Money.DefaultCurrency);

        var result = CashOperation.CreateFromSupplierPayment(
            number: number,
            supplierPaymentId: Guid.NewGuid(),
            supplierInvoiceNumber: "FS-2026-X",
            amount: amount,
            paymentDate: DateTime.UtcNow.Date);

        Assert.True(result.IsSuccess, result.Error?.Description);
        var cancelResult = result.Value.Cancel("Test");

        Assert.True(cancelResult.IsFailure);
        Assert.Equal("Validation.Origin", cancelResult.Error.Code);
    }
}
