using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class BankDepositDomainTests
{
    [Fact]
    public void BankDepositType_ToPaymentMethod_MapsExpectedValues()
    {
        Assert.Equal(PaymentMethod.Cash, BankDepositType.Cash.ToPaymentMethod());
        Assert.Equal(PaymentMethod.Check, BankDepositType.Check.ToPaymentMethod());
        Assert.Equal(PaymentMethod.BankTransfer, BankDepositType.Draft.ToPaymentMethod());
    }

    [Fact]
    public void Create_WithNonPositiveAmount_ShouldFail()
    {
        var num = BankDepositNumber.Create(2026, 1);
        Assert.True(num.IsSuccess);

        var cashOpId = Guid.NewGuid();
        var result = BankDeposit.Create(
            number: num.Value,
            depositType: BankDepositType.Cash,
            depositDate: DateTime.UtcNow.Date,
            bankAccountId: Guid.NewGuid(),
            amount: Money.Create(0m, Money.DefaultCurrency),
            quantity: 1,
            cashOperationId: cashOpId);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Amount", result.Error.Code);
    }

    [Fact]
    public void Create_WithQuantityZero_ShouldFail()
    {
        var num = BankDepositNumber.Create(2026, 1);
        Assert.True(num.IsSuccess);

        var result = BankDeposit.Create(
            number: num.Value,
            depositType: BankDepositType.Cash,
            depositDate: DateTime.UtcNow.Date,
            bankAccountId: Guid.NewGuid(),
            amount: Money.Create(10m, Money.DefaultCurrency),
            quantity: 0,
            cashOperationId: Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Quantity", result.Error.Code);
    }

    [Fact]
    public void Create_WithValidInput_ShouldSucceed()
    {
        var num = BankDepositNumber.Create(2026, 1);
        Assert.True(num.IsSuccess);

        var bankId = Guid.NewGuid();
        var cashOpId = Guid.NewGuid();

        var result = BankDeposit.Create(
            number: num.Value,
            depositType: BankDepositType.Check,
            depositDate: DateTime.UtcNow.Date,
            bankAccountId: bankId,
            amount: Money.Create(100.500m, Money.DefaultCurrency),
            quantity: 2,
            cashOperationId: cashOpId,
            depositSlipReference: "REF-001");

        Assert.True(result.IsSuccess);
        Assert.Equal(BankDepositType.Check, result.Value.DepositType);
        Assert.Equal(bankId, result.Value.BankAccountId);
        Assert.Equal(cashOpId, result.Value.CashOperationId);
        Assert.Equal(2, result.Value.Quantity);
    }
}
