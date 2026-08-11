using FactuTrust.Application.DTOs;
using FluentValidation;

namespace FactuTrust.Application.Features.FirmGovernance.Validation;

public sealed class FirmCollaboratorPayrollOnboardingValidator : AbstractValidator<FirmCollaboratorPayrollOnboardingDto>
{
    public FirmCollaboratorPayrollOnboardingValidator()
    {
        RuleFor(x => x.EmployeeNumber)
            .NotEmpty()
            .WithMessage("Le matricule du salarié est obligatoire.");

        RuleFor(x => x.HireDate)
            .NotEmpty()
            .WithMessage("La date d'embauche est obligatoire.");

        RuleFor(x => x.Contract)
            .NotNull()
            .WithMessage("Le contrat est obligatoire.");

        When(x => x.Contract is not null, () =>
        {
            RuleFor(x => x.Contract.StartDate)
                .NotEmpty()
                .WithMessage("La date de début du contrat est obligatoire.");

            RuleFor(x => x.Contract.BaseSalary)
                .GreaterThan(0)
                .WithMessage("Le salaire de base doit être supérieur à zéro.");

            RuleFor(x => x.Contract.WorkAccidentRate)
                .InclusiveBetween(0, 100)
                .WithMessage("Le taux d'accident de travail doit être compris entre 0 et 100 %.");

            RuleFor(x => x.Contract)
                .Must(c => c.Type != "Cdd" || c.EndDate.HasValue)
                .WithMessage("La date de fin est obligatoire pour un CDD.");
        });
    }
}
