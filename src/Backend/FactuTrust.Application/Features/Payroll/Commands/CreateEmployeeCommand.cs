using FactuTrust.Application.Common.Validation;
using FactuTrust.Application.Features.Payroll.Validation;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
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

public sealed class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, Result<Guid>>
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeDependentParentRepository _dependentParents;
    private readonly ITenantContext _tenantContext;
    private readonly IPayrollEmployeeChartProvisioningService _chartProvisioning;

    public CreateEmployeeCommandHandler(
        IEmployeeRepository employees,
        IEmployeeDependentParentRepository dependentParents,
        ITenantContext tenantContext,
        IPayrollEmployeeChartProvisioningService chartProvisioning)
    {
        _employees = employees;
        _dependentParents = dependentParents;
        _tenantContext = tenantContext;
        _chartProvisioning = chartProvisioning;
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

        var claims = dto.DependentParentClaims ?? Array.Empty<DependentParentClaimDto>();
        var useClaims = claims.Count > 0 || dto.DependentParents == 0;
        var dependentParentsCount = useClaims && claims.Count > 0
            ? claims.Count
            : (useClaims ? 0 : dto.DependentParents);

        // Création provisoire pour obtenir un Id, puis construction des claims.
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
            dependentParentsCount,
            dto.Category,
            dto.Echelon);

        if (employeeResult.IsFailure)
            return Result.Failure<Guid>(employeeResult.Error);

        var employee = employeeResult.Value;

        IReadOnlyList<EmployeeDependentParent> claimEntities = Array.Empty<EmployeeDependentParent>();
        if (claims.Count > 0)
        {
            var built = DependentParentClaimsHelper.BuildClaims(
                employee.Id, dto.Cin, claims, DateTime.UtcNow.Date);
            if (built.IsFailure)
                return Result.Failure<Guid>(built.Error);

            claimEntities = built.Value.Entities;
            var conflictCheck = await DependentParentClaimsHelper.EnsureNoConflictsAsync(
                _dependentParents, _employees, claimEntities, employee.Id, cancellationToken);
            if (conflictCheck.IsFailure)
                return Result.Failure<Guid>(conflictCheck.Error);

            employee.SyncDependentParentsCount(claimEntities.Count);
        }

        // Compte auxiliaire 425 alloué séquentiellement et rattaché au collectif : le compte est une
        // donnée de la fiche, et non plus une dérivation du matricule par troncature — plus de
        // collision possible, et le matricule cesse d'être recopié dans le plan comptable et le FEC.
        //
        // L'échec n'est plus avalé. Il l'était tant que la dérivation servait de repli ; celle-ci
        // ayant été supprimée, un salarié créé sans compte se heurterait au refus de validation de
        // son premier cycle de paie, loin de la cause. Mieux vaut refuser la création en nommant le
        // vrai problème : le plan comptable n'est pas initialisé.
        var auxiliary = await _chartProvisioning.AllocateAsync(employee.FullName, cancellationToken);
        if (auxiliary.IsFailure)
            return Result.Failure<Guid>(auxiliary.Error);

        // L'allocateur ne rend qu'un numéro libre dans le plan comptable ; ce contrôle couvre le cas
        // où une fiche porterait déjà ce compte sans qu'il existe au plan (reprise d'un bulletin figé).
        var auxiliaryCollision = await PayrollAuxiliaryAccountGuard.EnsureAccountNotTakenAsync(
            _employees, auxiliary.Value, excludeEmployeeId: null, cancellationToken);
        if (auxiliaryCollision.IsFailure)
            return Result.Failure<Guid>(auxiliaryCollision.Error);

        var assign = employee.SetAuxiliaryAccountNumber(auxiliary.Value);
        if (assign.IsFailure)
            return Result.Failure<Guid>(assign.Error);

        await _employees.AddAsync(employee, cancellationToken);

        if (claimEntities.Count > 0)
            await _dependentParents.ReplaceActiveClaimsAsync(employee.Id, claimEntities, cancellationToken);

        return Result.Success(employee.Id);
    }
}
