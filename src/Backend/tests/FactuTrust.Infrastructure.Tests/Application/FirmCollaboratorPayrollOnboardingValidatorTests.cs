using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.FirmGovernance.Validation;
using FluentValidation.TestHelper;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class FirmCollaboratorPayrollOnboardingValidatorTests
{
    private readonly FirmCollaboratorPayrollOnboardingValidator _validator = new();

    private static FirmCollaboratorPayrollOnboardingDto Valid() => new()
    {
        EmployeeNumber = "CAB-001",
        HireDate = new DateTime(2025, 1, 10),
        Contract = new CreateContractDto
        {
            Type = "Cdi",
            Regime = "Rsna",
            WeeklyRegime = "FortyEightHours",
            StartDate = new DateTime(2025, 1, 10),
            BaseSalary = 1500m,
            WorkAccidentRate = 0.4m
        }
    };

    [Fact]
    public void Valid_minimal_passes()
    {
        var result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_employee_number_fails()
    {
        var dto = Valid() with { EmployeeNumber = "" };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.EmployeeNumber)
            .WithErrorMessage("Le matricule du salarié est obligatoire.");
    }

    [Fact]
    public void Zero_base_salary_fails()
    {
        var dto = Valid() with
        {
            Contract = Valid().Contract with { BaseSalary = 0m }
        };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Contract.BaseSalary)
            .WithErrorMessage("Le salaire de base doit être supérieur à zéro.");
    }

    [Fact]
    public void Cdd_without_end_date_fails()
    {
        var dto = Valid() with
        {
            Contract = Valid().Contract with { Type = "Cdd", EndDate = null }
        };
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Contract)
            .WithErrorMessage("La date de fin est obligatoire pour un CDD.");
    }
}
