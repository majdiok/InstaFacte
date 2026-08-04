using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Clients.Commands;
using FactuTrust.Domain.Entities;
using FluentValidation.TestHelper;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class ClientCreditTermsValidatorTests
{
    private readonly UpdateClientCommandValidator _updateValidator = new();
    private readonly CreateClientCommandValidator _createValidator = new();

    [Fact]
    public void Update_ValidCreditTerms_Passes()
    {
        var cmd = ValidUpdate(creditLimit: 10_000m, paymentDays: 30);
        var result = _updateValidator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Dto.CreditLimit);
        result.ShouldNotHaveValidationErrorFor(x => x.Dto.DefaultPaymentTermDays);
    }

    [Fact]
    public void Update_NullCreditTerms_Passes()
    {
        var cmd = ValidUpdate(creditLimit: null, paymentDays: null);
        var result = _updateValidator.TestValidate(cmd);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Update_NegativeCreditLimit_Fails()
    {
        var cmd = ValidUpdate(creditLimit: -1m, paymentDays: null);
        var result = _updateValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Dto.CreditLimit)
            .WithErrorMessage("Le plafond d'encours ne peut pas être négatif");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void Update_PaymentDaysOutOfRange_Fails(int days)
    {
        var cmd = ValidUpdate(creditLimit: null, paymentDays: days);
        var result = _updateValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Dto.DefaultPaymentTermDays)
            .WithErrorMessage("Le délai doit être compris entre 0 et 365 jours");
    }

    [Fact]
    public void Create_NegativeCreditLimit_Fails()
    {
        var cmd = ValidCreate(creditLimit: -5m, paymentDays: 10);
        var result = _createValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Dto.CreditLimit);
    }

    [Fact]
    public void Create_ValidCreditTerms_Passes()
    {
        var cmd = ValidCreate(creditLimit: 1_000m, paymentDays: 45);
        var result = _createValidator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Dto.CreditLimit);
        result.ShouldNotHaveValidationErrorFor(x => x.Dto.DefaultPaymentTermDays);
    }

    private static UpdateClientCommand ValidUpdate(decimal? creditLimit, int? paymentDays) =>
        new(Guid.NewGuid(), new UpdateClientDto
        {
            Name = "Client test",
            Email = "client@test.local",
            Street = "1 rue Test",
            City = "Tunis",
            Governorate = "Tunis",
            IsActive = true,
            CreditLimit = creditLimit,
            DefaultPaymentTermDays = paymentDays
        });

    private static CreateClientCommand ValidCreate(decimal? creditLimit, int? paymentDays) =>
        new(new CreateClientDto
        {
            Name = "Client test",
            Type = ClientType.Individual,
            Email = "client@test.local",
            Street = "1 rue Test",
            City = "Tunis",
            Governorate = "Tunis",
            CreditLimit = creditLimit,
            DefaultPaymentTermDays = paymentDays
        });
}
