using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Advances;

// ── Create advance ──
public sealed record CreateAdvanceCommand(CreateAdvanceDto Dto) : IRequest<Result<Guid>>;

public sealed class CreateAdvanceCommandValidator : AbstractValidator<CreateAdvanceCommand>
{
    public CreateAdvanceCommandValidator()
    {
        RuleFor(x => x.Dto.EmployeeId).NotEmpty();
        RuleFor(x => x.Dto.Amount).GreaterThan(0).WithMessage("Le montant de l'avance doit être strictement positif.");
    }
}

public sealed class CreateAdvanceCommandHandler : IRequestHandler<CreateAdvanceCommand, Result<Guid>>
{
    private readonly IEmployeeAdvanceRepository _advances;
    private readonly IEmployeeRepository _employees;

    public CreateAdvanceCommandHandler(IEmployeeAdvanceRepository advances, IEmployeeRepository employees)
    {
        _advances = advances;
        _employees = employees;
    }

    public async Task<Result<Guid>> Handle(CreateAdvanceCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        if (!await _employees.ExistsAsync(dto.EmployeeId, cancellationToken))
            return Result.Failure<Guid>(Error.NotFound("Employee", dto.EmployeeId));

        var advanceResult = EmployeeAdvance.Create(dto.EmployeeId, dto.Date, dto.Amount, dto.Reason);
        if (advanceResult.IsFailure)
            return Result.Failure<Guid>(advanceResult.Error);

        await _advances.AddAsync(advanceResult.Value, cancellationToken);
        return Result.Success(advanceResult.Value.Id);
    }
}

// ── Delete advance ──
public sealed record DeleteAdvanceCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteAdvanceCommandHandler : IRequestHandler<DeleteAdvanceCommand, Result>
{
    private readonly IEmployeeAdvanceRepository _advances;

    public DeleteAdvanceCommandHandler(IEmployeeAdvanceRepository advances)
    {
        _advances = advances;
    }

    public async Task<Result> Handle(DeleteAdvanceCommand request, CancellationToken cancellationToken)
    {
        var advance = await _advances.GetByIdAsync(request.Id, cancellationToken);
        if (advance is null)
            return Result.Failure(Error.NotFound("EmployeeAdvance", request.Id));

        if (advance.IsSettled)
            return Result.Failure(Error.Validation("IsSettled", "Une avance déjà retenue sur un bulletin ne peut pas être supprimée."));

        await _advances.DeleteAsync(advance, cancellationToken);
        return Result.Success();
    }
}

// ── List advances by employee ──
public sealed record GetEmployeeAdvancesQuery(Guid EmployeeId) : IRequest<IReadOnlyList<EmployeeAdvanceDto>>;

public sealed class GetEmployeeAdvancesQueryHandler : IRequestHandler<GetEmployeeAdvancesQuery, IReadOnlyList<EmployeeAdvanceDto>>
{
    private readonly IEmployeeAdvanceRepository _advances;

    public GetEmployeeAdvancesQueryHandler(IEmployeeAdvanceRepository advances)
    {
        _advances = advances;
    }

    public async Task<IReadOnlyList<EmployeeAdvanceDto>> Handle(GetEmployeeAdvancesQuery request, CancellationToken cancellationToken)
    {
        var advances = await _advances.ListByEmployeeAsync(request.EmployeeId, cancellationToken);
        return advances.Select(a => PayrollMappings.ToDto(a)).ToList();
    }
}
