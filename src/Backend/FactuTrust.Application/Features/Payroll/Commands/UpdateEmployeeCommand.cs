using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Validation;
using FactuTrust.Domain.Common;
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

    public UpdateEmployeeCommandHandler(IEmployeeRepository employees)
    {
        _employees = employees;
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
            dto.DependentParents);

        if (updateResult.IsFailure)
            return updateResult;

        await _employees.UpdateAsync(employee, cancellationToken);
        return Result.Success();
    }
}
