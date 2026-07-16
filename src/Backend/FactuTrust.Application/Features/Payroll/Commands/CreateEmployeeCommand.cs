using FactuTrust.Application.Common.Validation;
using FactuTrust.Application.Features.Payroll.Validation;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Commands;

public sealed record CreateEmployeeCommand(CreateEmployeeDto Dto) : IRequest<Result<Guid>>;

public sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.Dto.EmployeeNumber).NotEmpty().WithMessage("Le matricule du salarié est obligatoire.");
        RuleFor(x => x.Dto.FirstName).NotEmpty().WithMessage("Le prénom est obligatoire.");
        RuleFor(x => x.Dto.LastName).NotEmpty().WithMessage("Le nom est obligatoire.");
        RuleFor(x => x.Dto.HireDate).NotEmpty().WithMessage("La date d'embauche est obligatoire.");
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

public sealed class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, Result<Guid>>
{
    private readonly IEmployeeRepository _employees;
    private readonly ITenantContext _tenantContext;

    public CreateEmployeeCommandHandler(IEmployeeRepository employees, ITenantContext tenantContext)
    {
        _employees = employees;
        _tenantContext = tenantContext;
    }

    public async Task<Result<Guid>> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        if (_tenantContext.TenantId is null)
            return Result.Failure<Guid>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        if (!Enum.TryParse<MaritalStatus>(dto.MaritalStatus, out var maritalStatus))
            return Result.Failure<Guid>(Error.Validation("MaritalStatus", "Situation familiale invalide."));

        if (await _employees.ExistsByEmployeeNumberAsync(dto.EmployeeNumber, null, cancellationToken))
            return Result.Failure<Guid>(Error.Conflict("Un salarié existe déjà avec ce matricule."));

        Address? address = null;
        if (!string.IsNullOrWhiteSpace(dto.Street) && !string.IsNullOrWhiteSpace(dto.City) && !string.IsNullOrWhiteSpace(dto.Governorate))
        {
            var addressResult = Address.Create(dto.Street!, dto.City!, dto.Governorate!, dto.StreetLine2, dto.PostalCode);
            if (addressResult.IsFailure)
                return Result.Failure<Guid>(addressResult.Error);
            address = addressResult.Value;
        }

        Email? email = null;
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var emailResult = Email.Create(dto.Email!.Trim());
            if (emailResult.IsFailure)
                return Result.Failure<Guid>(emailResult.Error);
            email = emailResult.Value;
        }

        PhoneNumber? phone = null;
        if (!string.IsNullOrWhiteSpace(dto.Phone))
        {
            var phoneResult = PhoneNumber.Create(dto.Phone!.Trim());
            if (phoneResult.IsFailure)
                return Result.Failure<Guid>(phoneResult.Error);
            phone = phoneResult.Value;
        }

        var employeeResult = Employee.Create(
            dto.EmployeeNumber,
            dto.FirstName,
            dto.LastName,
            dto.HireDate,
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

        if (employeeResult.IsFailure)
            return Result.Failure<Guid>(employeeResult.Error);

        var employee = employeeResult.Value;
        await _employees.AddAsync(employee, cancellationToken);

        return Result.Success(employee.Id);
    }
}
