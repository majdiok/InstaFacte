using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.EmployeeLoans;

public sealed record ListEmployeeLoansQuery(Guid EmployeeId) : IRequest<IReadOnlyList<EmployeeLoanDto>>;

public sealed class ListEmployeeLoansQueryHandler : IRequestHandler<ListEmployeeLoansQuery, IReadOnlyList<EmployeeLoanDto>>
{
    private readonly IEmployeeLoanRepository _loans;
    private readonly IEmployeeRepository _employees;

    public ListEmployeeLoansQueryHandler(IEmployeeLoanRepository loans, IEmployeeRepository employees)
    {
        _loans = loans;
        _employees = employees;
    }

    public async Task<IReadOnlyList<EmployeeLoanDto>> Handle(ListEmployeeLoansQuery request, CancellationToken cancellationToken)
    {
        var loans = await _loans.ListByEmployeeAsync(request.EmployeeId, cancellationToken);
        var employee = await _employees.GetByIdAsync(request.EmployeeId, cancellationToken);
        return loans.Select(l => PayrollMappings.ToEmployeeLoanDto(l, employee?.FullName)).ToList();
    }
}

public sealed record GetEmployeeLoanByIdQuery(Guid Id) : IRequest<Result<EmployeeLoanDto>>;

public sealed class GetEmployeeLoanByIdQueryHandler : IRequestHandler<GetEmployeeLoanByIdQuery, Result<EmployeeLoanDto>>
{
    private readonly IEmployeeLoanRepository _loans;
    private readonly IEmployeeRepository _employees;

    public GetEmployeeLoanByIdQueryHandler(IEmployeeLoanRepository loans, IEmployeeRepository employees)
    {
        _loans = loans;
        _employees = employees;
    }

    public async Task<Result<EmployeeLoanDto>> Handle(GetEmployeeLoanByIdQuery request, CancellationToken cancellationToken)
    {
        var loan = await _loans.GetByIdWithInstallmentsAsync(request.Id, cancellationToken);
        if (loan is null)
            return Result.Failure<EmployeeLoanDto>(Error.NotFound("EmployeeLoan", request.Id));

        var employee = await _employees.GetByIdAsync(loan.EmployeeId, cancellationToken);
        return Result.Success(PayrollMappings.ToEmployeeLoanDto(loan, employee?.FullName));
    }
}

public sealed record CreateEmployeeLoanCommand(CreateEmployeeLoanDto Dto) : IRequest<Result<Guid>>;

public sealed class CreateEmployeeLoanCommandValidator : AbstractValidator<CreateEmployeeLoanCommand>
{
    public CreateEmployeeLoanCommandValidator()
    {
        RuleFor(x => x.Dto.EmployeeId).NotEmpty();
        RuleFor(x => x.Dto.Reference).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Dto.Principal).GreaterThan(0);
        RuleFor(x => x.Dto.InstallmentCount).GreaterThan(0);
        RuleFor(x => x.Dto.StartMonth).InclusiveBetween(1, 12);
    }
}

public sealed class CreateEmployeeLoanCommandHandler : IRequestHandler<CreateEmployeeLoanCommand, Result<Guid>>
{
    private readonly IEmployeeLoanRepository _loans;
    private readonly IEmployeeRepository _employees;

    public CreateEmployeeLoanCommandHandler(IEmployeeLoanRepository loans, IEmployeeRepository employees)
    {
        _loans = loans;
        _employees = employees;
    }

    public async Task<Result<Guid>> Handle(CreateEmployeeLoanCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        if (await _employees.GetByIdAsync(dto.EmployeeId, cancellationToken) is null)
            return Result.Failure<Guid>(Error.NotFound("Employee", dto.EmployeeId));

        var result = EmployeeLoan.Create(
            dto.EmployeeId, dto.Reference, dto.Principal, dto.InstallmentCount,
            dto.StartYear, dto.StartMonth, dto.Notes);

        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        await _loans.AddAsync(result.Value, cancellationToken);
        return Result.Success(result.Value.Id);
    }
}

public sealed record CancelEmployeeLoanCommand(Guid Id) : IRequest<Result>;

public sealed class CancelEmployeeLoanCommandHandler : IRequestHandler<CancelEmployeeLoanCommand, Result>
{
    private readonly IEmployeeLoanRepository _loans;

    public CancelEmployeeLoanCommandHandler(IEmployeeLoanRepository loans) => _loans = loans;

    public async Task<Result> Handle(CancelEmployeeLoanCommand request, CancellationToken cancellationToken)
    {
        var loan = await _loans.GetByIdWithInstallmentsAsync(request.Id, cancellationToken);
        if (loan is null)
            return Result.Failure(Error.NotFound("EmployeeLoan", request.Id));

        try
        {
            loan.Cancel();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.Validation("Status", ex.Message));
        }

        await _loans.UpdateAsync(loan, cancellationToken);
        return Result.Success();
    }
}
