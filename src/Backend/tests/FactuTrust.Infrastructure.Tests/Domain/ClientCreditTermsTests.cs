using FactuTrust.Domain.Entities;
using FactuTrust.Domain.ValueObjects;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain;

public sealed class ClientCreditTermsTests
{
    [Fact]
    public void SetCreditTerms_AcceptsLimitAndPaymentDays()
    {
        var client = NewClient();

        var result = client.SetCreditTerms(10_000m, 30);

        Assert.True(result.IsSuccess);
        Assert.Equal(10_000m, client.CreditLimit);
        Assert.Equal(30, client.DefaultPaymentTermDays);
    }

    [Fact]
    public void SetCreditTerms_NullLimit_ClearsPlafond()
    {
        var client = NewClient();
        Assert.True(client.SetCreditTerms(5_000m, 15).IsSuccess);

        var result = client.SetCreditTerms(null, null);

        Assert.True(result.IsSuccess);
        Assert.Null(client.CreditLimit);
        Assert.Null(client.DefaultPaymentTermDays);
    }

    [Fact]
    public void SetCreditTerms_NegativeLimit_Fails()
    {
        var client = NewClient();

        var result = client.SetCreditTerms(-1m, null);

        Assert.True(result.IsFailure);
        Assert.Null(client.CreditLimit);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void SetCreditTerms_PaymentDaysOutOfRange_Fails(int days)
    {
        var client = NewClient();

        var result = client.SetCreditTerms(null, days);

        Assert.True(result.IsFailure);
        Assert.Null(client.DefaultPaymentTermDays);
    }

    [Fact]
    public void SetCreditTerms_ZeroLimitAndZeroDays_Succeeds()
    {
        var client = NewClient();

        var result = client.SetCreditTerms(0m, 0);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, client.CreditLimit);
        Assert.Equal(0, client.DefaultPaymentTermDays);
    }

    private static Client NewClient()
    {
        var address = Address.Create("1 rue Test", "Tunis", "Tunis").Value;
        var email = Email.Create("client@test.local").Value;
        return Client.Create("Client test", ClientType.Individual, address, email).Value;
    }
}
