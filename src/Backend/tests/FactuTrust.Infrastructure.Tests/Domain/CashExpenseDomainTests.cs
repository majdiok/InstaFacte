using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class CashOperationDomainTests
{
    [Fact]
    public void Create_WithNonPositiveAmount_ShouldFail()
    {
        var year = DateTime.UtcNow.Year;
        var numberResult = CashOperationNumber.Create("DEP", year, 1);
        Assert.True(numberResult.IsSuccess, numberResult.Error?.Description);

        var result = CashOperation.Create(
            number: numberResult.Value,
            operationType: CashOperationType.Debit,
            operationDate: DateTime.UtcNow.Date,
            method: PaymentMethod.Cash,
            amount: Money.Create(0m, Money.DefaultCurrency),
            label: "Test",
            category: CashExpenseCategory.Other);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Amount", result.Error.Code);
    }

    [Fact]
    public void Create_WithFutureDate_ShouldFail()
    {
        var year = DateTime.UtcNow.Year;
        var numberResult = CashOperationNumber.Create("DEP", year, 1);
        Assert.True(numberResult.IsSuccess, numberResult.Error?.Description);

        var futureDate = DateTime.UtcNow.Date.AddDays(2);

        var result = CashOperation.Create(
            number: numberResult.Value,
            operationType: CashOperationType.Debit,
            operationDate: futureDate,
            method: PaymentMethod.Cash,
            amount: Money.Create(10m, Money.DefaultCurrency),
            label: "Test",
            category: CashExpenseCategory.Other);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.OperationDate", result.Error.Code);
    }

    [Fact]
    public void Create_WithInvalidPaymentMethod_ShouldFail()
    {
        var year = DateTime.UtcNow.Year;
        var numberResult = CashOperationNumber.Create("DEP", year, 1);
        Assert.True(numberResult.IsSuccess, numberResult.Error?.Description);

        var invalidMethod = (PaymentMethod)12345;

        var result = CashOperation.Create(
            number: numberResult.Value,
            operationType: CashOperationType.Debit,
            operationDate: DateTime.UtcNow.Date,
            method: invalidMethod,
            amount: Money.Create(10m, Money.DefaultCurrency),
            label: "Test",
            category: CashExpenseCategory.Other);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Method", result.Error.Code);
    }

    [Fact]
    public void Create_DebitWithoutCategory_ShouldFail()
    {
        var year = DateTime.UtcNow.Year;
        var numberResult = CashOperationNumber.Create("DEP", year, 1);
        Assert.True(numberResult.IsSuccess, numberResult.Error?.Description);

        var result = CashOperation.Create(
            number: numberResult.Value,
            operationType: CashOperationType.Debit,
            operationDate: DateTime.UtcNow.Date,
            method: PaymentMethod.Cash,
            amount: Money.Create(10m, Money.DefaultCurrency),
            label: "Test");

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Category", result.Error.Code);
    }

    [Fact]
    public void Create_CreditWithoutRevenueCategory_ShouldFail()
    {
        var year = DateTime.UtcNow.Year;
        var numberResult = CashOperationNumber.Create("ENC", year, 1);
        Assert.True(numberResult.IsSuccess, numberResult.Error?.Description);

        var result = CashOperation.Create(
            number: numberResult.Value,
            operationType: CashOperationType.Credit,
            operationDate: DateTime.UtcNow.Date,
            method: PaymentMethod.Cash,
            amount: Money.Create(10m, Money.DefaultCurrency),
            label: "Test");

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.RevenueCategory", result.Error.Code);
    }

    [Fact]
    public void Create_CreditWithRevenueCategory_ShouldSucceed()
    {
        var year = DateTime.UtcNow.Year;
        var numberResult = CashOperationNumber.Create("ENC", year, 1);
        Assert.True(numberResult.IsSuccess, numberResult.Error?.Description);

        var result = CashOperation.Create(
            number: numberResult.Value,
            operationType: CashOperationType.Credit,
            operationDate: DateTime.UtcNow.Date,
            method: PaymentMethod.Cash,
            amount: Money.Create(100m, Money.DefaultCurrency),
            label: "Encaissement ventes",
            revenueCategory: CashRevenueCategory.CashSalesReceipt);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(CashOperationType.Credit, result.Value.OperationType);
        Assert.Equal(CashRevenueCategory.CashSalesReceipt, result.Value.RevenueCategory);
        Assert.Null(result.Value.Category);
    }

    [Theory]
    [InlineData(CashOperationType.Debit)]
    [InlineData(CashOperationType.Credit)]
    public void Create_WithTraite_ShouldFail(CashOperationType operationType)
    {
        var prefix = operationType == CashOperationType.Debit
            ? CashOperationNumber.DebitPrefix
            : CashOperationNumber.CreditPrefix;
        var numberResult = CashOperationNumber.Create(prefix, DateTime.UtcNow.Year, 1);
        Assert.True(numberResult.IsSuccess, numberResult.Error?.Description);

        var result = CashOperation.Create(
            number: numberResult.Value,
            operationType: operationType,
            operationDate: DateTime.UtcNow.Date,
            method: PaymentMethod.Traite,
            amount: Money.Create(100m, Money.DefaultCurrency),
            label: "Test",
            category: operationType == CashOperationType.Debit ? CashExpenseCategory.Other : null,
            revenueCategory: operationType == CashOperationType.Credit ? CashRevenueCategory.Other : null);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Method", result.Error.Code);
    }

    [Theory]
    [InlineData(PaymentMethod.Cash)]
    [InlineData(PaymentMethod.BankTransfer)]
    [InlineData(PaymentMethod.Check)]
    [InlineData(PaymentMethod.Card)]
    [InlineData(PaymentMethod.MobilePayment)]
    [InlineData(PaymentMethod.Other)]
    public void Create_WithNonTraiteMethod_ShouldSucceed(PaymentMethod method)
    {
        var numberResult = CashOperationNumber.Create(CashOperationNumber.DebitPrefix, DateTime.UtcNow.Year, 1);
        Assert.True(numberResult.IsSuccess, numberResult.Error?.Description);

        var result = CashOperation.Create(
            number: numberResult.Value,
            operationType: CashOperationType.Debit,
            operationDate: DateTime.UtcNow.Date,
            method: method,
            amount: Money.Create(100m, Money.DefaultCurrency),
            label: "Test",
            category: CashExpenseCategory.Other);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(method, result.Value.Method);
    }

    // ─────────────── Volet TVA (§9.4) ───────────────

    [Fact]
    public void Create_VatRateOnDebit_ShouldFail()
    {
        var numberResult = CashOperationNumber.Create(CashOperationNumber.DebitPrefix, DateTime.UtcNow.Year, 1);
        Assert.True(numberResult.IsSuccess, numberResult.Error?.Description);

        var result = CashOperation.Create(
            number: numberResult.Value,
            operationType: CashOperationType.Debit,
            operationDate: DateTime.UtcNow.Date,
            method: PaymentMethod.Cash,
            amount: Money.Create(100m, Money.DefaultCurrency),
            label: "Test",
            category: CashExpenseCategory.Other,
            vatRate: VatRate.Standard);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.VatRate", result.Error.Code);
    }

    [Theory]
    [InlineData(CashRevenueCategory.ClientReceivablesReceipt)]
    [InlineData(CashRevenueCategory.PartnerContributionsReceipt)]
    [InlineData(CashRevenueCategory.BankCreditReceipt)]
    [InlineData(CashRevenueCategory.Other)]
    public void Create_VatRateOnCreditWithNonCashSalesCategory_ShouldFail(CashRevenueCategory category)
    {
        var numberResult = CashOperationNumber.Create(CashOperationNumber.CreditPrefix, DateTime.UtcNow.Year, 1);
        Assert.True(numberResult.IsSuccess, numberResult.Error?.Description);

        var result = CashOperation.Create(
            number: numberResult.Value,
            operationType: CashOperationType.Credit,
            operationDate: DateTime.UtcNow.Date,
            method: PaymentMethod.Cash,
            amount: Money.Create(100m, Money.DefaultCurrency),
            label: "Test",
            revenueCategory: category,
            vatRate: VatRate.Standard);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.VatRate", result.Error.Code);
    }

    [Theory]
    [InlineData(VatRate.Exempt)]
    [InlineData(VatRate.Reduced)]
    [InlineData(VatRate.Intermediate)]
    [InlineData(VatRate.Standard)]
    public void Create_VatRateOnCreditCashSalesReceipt_ShouldSucceed(VatRate rate)
    {
        var numberResult = CashOperationNumber.Create(CashOperationNumber.CreditPrefix, DateTime.UtcNow.Year, 1);
        Assert.True(numberResult.IsSuccess, numberResult.Error?.Description);

        var result = CashOperation.Create(
            number: numberResult.Value,
            operationType: CashOperationType.Credit,
            operationDate: DateTime.UtcNow.Date,
            method: PaymentMethod.Cash,
            amount: Money.Create(100m, Money.DefaultCurrency),
            label: "Test",
            revenueCategory: CashRevenueCategory.CashSalesReceipt,
            vatRate: rate);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(rate, result.Value.VatRate);
    }

    [Fact]
    public void Create_VatRateNull_ShouldSucceedRegardlessOfCategory()
    {
        var numberResult = CashOperationNumber.Create(CashOperationNumber.DebitPrefix, DateTime.UtcNow.Year, 1);
        Assert.True(numberResult.IsSuccess, numberResult.Error?.Description);

        var result = CashOperation.Create(
            number: numberResult.Value,
            operationType: CashOperationType.Debit,
            operationDate: DateTime.UtcNow.Date,
            method: PaymentMethod.Cash,
            amount: Money.Create(100m, Money.DefaultCurrency),
            label: "Test",
            category: CashExpenseCategory.Other,
            vatRate: null);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Null(result.Value.VatRate);
    }

    [Fact]
    public void Create_InvalidVatRateEnumValue_ShouldFail()
    {
        var numberResult = CashOperationNumber.Create(CashOperationNumber.CreditPrefix, DateTime.UtcNow.Year, 1);
        Assert.True(numberResult.IsSuccess, numberResult.Error?.Description);

        var invalidRate = (VatRate)55;

        var result = CashOperation.Create(
            number: numberResult.Value,
            operationType: CashOperationType.Credit,
            operationDate: DateTime.UtcNow.Date,
            method: PaymentMethod.Cash,
            amount: Money.Create(100m, Money.DefaultCurrency),
            label: "Test",
            revenueCategory: CashRevenueCategory.CashSalesReceipt,
            vatRate: invalidRate);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.VatRate", result.Error.Code);
    }

    [Fact]
    public void CreateFromInvoicePayment_NeverSetsVatRate()
    {
        var number = CashOperationNumber.Create(CashOperationNumber.CreditPrefix, DateTime.UtcNow.Year, 1).Value;
        var result = CashOperation.CreateFromInvoicePayment(
            number: number,
            paymentId: Guid.NewGuid(),
            invoiceNumber: "FAC-1",
            amount: Money.Create(100m, Money.DefaultCurrency),
            paymentDate: DateTime.UtcNow.Date);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Null(result.Value.VatRate);
    }

    [Fact]
    public void CreateFromSupplierPayment_NeverSetsVatRate()
    {
        var number = CashOperationNumber.Create(CashOperationNumber.DebitPrefix, DateTime.UtcNow.Year, 1).Value;
        var result = CashOperation.CreateFromSupplierPayment(
            number: number,
            supplierPaymentId: Guid.NewGuid(),
            supplierInvoiceNumber: "FS-1",
            amount: Money.Create(100m, Money.DefaultCurrency),
            paymentDate: DateTime.UtcNow.Date);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Null(result.Value.VatRate);
    }

    [Fact]
    public void CreateFromInvoiceRefund_NeverSetsVatRate()
    {
        var number = CashOperationNumber.Create(CashOperationNumber.DebitPrefix, DateTime.UtcNow.Year, 1).Value;
        var result = CashOperation.CreateFromInvoiceRefund(
            number: number,
            paymentId: Guid.NewGuid(),
            invoiceNumber: "FAC-2",
            amount: Money.Create(100m, Money.DefaultCurrency),
            paymentDate: DateTime.UtcNow.Date);

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Null(result.Value.VatRate);
    }

    [Fact]
    public void Cancel_Twice_ShouldSecondCancellationFail()
    {
        var year = DateTime.UtcNow.Year;
        var numberResult = CashOperationNumber.Create("DEP", year, 1);
        Assert.True(numberResult.IsSuccess, numberResult.Error?.Description);

        var operationResult = CashOperation.Create(
            number: numberResult.Value,
            operationType: CashOperationType.Debit,
            operationDate: DateTime.UtcNow.Date,
            method: PaymentMethod.Cash,
            amount: Money.Create(10m, Money.DefaultCurrency),
            label: "Test",
            category: CashExpenseCategory.Other);
        Assert.True(operationResult.IsSuccess, operationResult.Error?.Description);

        var operation = operationResult.Value;

        var first = operation.Cancel("Annulation 1");
        Assert.True(first.IsSuccess, first.Error?.Description);

        var second = operation.Cancel("Annulation 2");
        Assert.True(second.IsFailure);
        Assert.Equal("Validation.Status", second.Error.Code);
    }
}
