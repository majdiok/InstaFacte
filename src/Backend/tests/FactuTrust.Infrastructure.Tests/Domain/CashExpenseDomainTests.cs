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
