using FactuTrust.Application.Common.Validation;
using FluentValidation;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

public sealed class PayrollValidatorsTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("12345678", true)]
    [InlineData("1234567", false)]
    [InlineData("123456789", false)]
    [InlineData("ABCDEFGH", false)]
    public void IsValidCin_accepts_eight_digits_only(string? cin, bool expected)
    {
        Assert.Equal(expected, TunisianValidationRules.IsValidCin(cin));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("1234567890", true)]
    [InlineData("123456789", false)]
    [InlineData("12345678901", false)]
    public void IsValidCnssNumber_accepts_ten_digits(string? cnss, bool expected)
    {
        Assert.Equal(expected, TunisianValidationRules.IsValidCnssNumber(cnss));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("12345678901234567890", true)]
    [InlineData("1234567890123456789", false)]
    public void IsValidRib_accepts_twenty_digits(string? rib, bool expected)
    {
        Assert.Equal(expected, TunisianValidationRules.IsValidRib(rib));
    }

    [Theory]
    [InlineData("Tunis", true)]
    [InlineData("Sfax", true)]
    [InlineData("Paris", false)]
    public void IsValidGovernorate_checks_whitelist(string governorate, bool expected)
    {
        Assert.Equal(expected, TunisianValidationRules.IsValidGovernorate(governorate));
    }
}
