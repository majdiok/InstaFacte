using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Commands;
using FactuTrust.Application.Features.Accounting.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class CreateSubAccountCommandValidatorTests
{
    private readonly CreateSubAccountCommandValidator _validator = new();

    [Theory]
    [InlineData("428.3")]
    [InlineData("425.1")]
    [InlineData("421.1")]
    [InlineData("41100001")]
    [InlineData("42")]
    public void AccountNumber_ValidSce_Passes(string accountNumber)
    {
        var result = _validator.TestValidate(Command(accountNumber));
        result.ShouldNotHaveValidationErrorFor(x => x.Request.AccountNumber);
    }

    [Theory]
    [InlineData("428.")]
    [InlineData(".428")]
    [InlineData("42..1")]
    [InlineData("A28")]
    [InlineData("")]
    public void AccountNumber_InvalidSce_Fails(string accountNumber)
    {
        var result = _validator.TestValidate(Command(accountNumber));
        result.ShouldHaveValidationErrorFor(x => x.Request.AccountNumber);
    }

    private static CreateSubAccountCommand Command(string accountNumber) =>
        new(new CreateSubAccountRequest
        {
            AccountNumber = accountNumber,
            Label = "Personnel — mutuelle",
            AccountClass = 4,
            NatureType = 2,
            AccountType = 3
        });
}
