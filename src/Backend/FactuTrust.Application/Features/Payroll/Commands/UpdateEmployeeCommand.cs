using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Validation;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Commands;

public sealed record UpdateEmployeeCommand(Guid Id, UpdateEmployeeDto Dto) : IRequest<Result>;

public sealed class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator()
    {
        RuleFor(x => x.Dto.FirstName).NotEmpty().WithMessage("Le prénom est obligatoire.");
        RuleFor(x => x.Dto.LastName).NotEmpty().WithMessage("Le nom est obligatoire.");
        RuleFor(x => x.Dto.DependentChildren).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Dto.StudentChildren).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Dto.DisabledChildren).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Dto.DependentParents).InclusiveBetween(0, 2)
            .WithMessage("Le nombre de parents à charge doit être compris entre 0 et 2.");
        RuleFor(x => x.Dto.DependentParentClaims).Must(c => c is null || c.Count <= 2)
            .WithMessage("Un salarié ne peut déclarer que 2 parents à charge au maximum.");
        RuleForEach(x => x.Dto.DependentParentClaims).ChildRules(claim =>
        {
            claim.RuleFor(c => c.ParentCin).NotEmpty().WithMessage("Le CIN du parent est obligatoire.").ValidCin();
            claim.RuleFor(c => c.Kinship).NotEmpty();
        });
        RuleFor(x => x.Dto)
            .Must(d => d.StudentChildren + d.DisabledChildren <= d.DependentChildren)
            .WithMessage("Le total des enfants étudiants et infirmes ne peut pas dépasser le nombre d'enfants à charge.");
        RuleFor(x => x.Dto.Cin).ValidCin();
        RuleFor(x => x.Dto.CnssNumber).ValidCnss();
        RuleFor(x => x.Dto.Rib).ValidRib();
        RuleFor(x => x.Dto.Governorate).ValidGovernorate();
    }
}

public sealed class UpdateEmployeeCommandHandler : IRequestHandler<UpdateEmployeeCommand, Result>
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeDependentParentRepository _dependentParents;

    public UpdateEmployeeCommandHandler(
        IEmployeeRepository employees,
        IEmployeeDependentParentRepository dependentParents)
    {
        _employees = employees;
        _dependentParents = dependentParents;
    }

    public async Task<Result> Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        var employee = await _employees.GetByIdAsync(request.Id, cancellationToken);
        if (employee is null)
            return Result.Failure(Error.NotFound("Employee", request.Id));

        if (!Enum.TryParse<MaritalStatus>(dto.MaritalStatus, out var maritalStatus))
            return Result.Failure(Error.Validation("MaritalStatus", "Situation familiale invalide."));

        Address? address = null;
        if (!string.IsNullOrWhiteSpace(dto.Street) && !string.IsNullOrWhiteSpace(dto.City) && !string.IsNullOrWhiteSpace(dto.Governorate))
        {
            var addressResult = Address.Create(dto.Street!, dto.City!, dto.Governorate!, dto.StreetLine2, dto.PostalCode);
            if (addressResult.IsFailure)
                return Result.Failure(addressResult.Error);
            address = addressResult.Value;
        }

        Email? email = null;
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var emailResult = Email.Create(dto.Email!.Trim());
            if (emailResult.IsFailure)
                return Result.Failure(emailResult.Error);
            email = emailResult.Value;
        }

        PhoneNumber? phone = null;
        if (!string.IsNullOrWhiteSpace(dto.Phone))
        {
            var phoneResult = PhoneNumber.Create(dto.Phone!.Trim());
            if (phoneResult.IsFailure)
                return Result.Failure(phoneResult.Error);
            phone = phoneResult.Value;
        }

        // Les claims fournis (y compris liste vide) font autorité ; sinon on conserve le compteur legacy.
        var claimsProvided = dto.DependentParentClaims is not null;
        var claims = dto.DependentParentClaims ?? Array.Empty<DependentParentClaimDto>();
        var dependentParentsCount = claimsProvided ? claims.Count : dto.DependentParents;

        IReadOnlyList<EmployeeDependentParent> claimEntities = Array.Empty<EmployeeDependentParent>();
        if (claimsProvided)
        {
            var built = DependentParentClaimsHelper.BuildClaims(
                employee.Id, dto.Cin ?? employee.Cin, claims, DateTime.UtcNow.Date);
            if (built.IsFailure)
                return Result.Failure(built.Error);

            claimEntities = built.Value.Entities;
            var conflictCheck = await DependentParentClaimsHelper.EnsureNoConflictsAsync(
                _dependentParents, _employees, claimEntities, employee.Id, cancellationToken);
            if (conflictCheck.IsFailure)
                return conflictCheck;
        }

        var updateResult = employee.Update(
            dto.FirstName,
            dto.LastName,
            maritalStatus,
            dto.IsHeadOfFamily,
            dto.DependentChildren,
            dto.Cin,
            dto.CnssNumber,
            dto.DateOfBirth,
            address,
            email,
            phone,
            dto.Rib,
            dto.StudentChildren,
            dto.DisabledChildren,
            dependentParentsCount,
            dto.Category,
            dto.Echelon);

        if (updateResult.IsFailure)
            return updateResult;

        if (claimsProvided)
        {
            var sync = employee.SyncDependentParentsCount(claimEntities.Count);
            if (sync.IsFailure)
                return sync;

            await _dependentParents.ReplaceActiveClaimsAsync(employee.Id, claimEntities, cancellationToken);
        }

        await _employees.UpdateAsync(employee, cancellationToken);
        return Result.Success();
    }
}
