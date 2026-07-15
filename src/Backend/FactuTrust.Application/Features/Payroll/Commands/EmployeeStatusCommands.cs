using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Commands;

public sealed record ToggleEmployeeActiveCommand(Guid Id) : IRequest<Result<bool>>;

public sealed class ToggleEmployeeActiveCommandHandler : IRequestHandler<ToggleEmployeeActiveCommand, Result<bool>>
{
    private readonly IEmployeeRepository _employees;

    public ToggleEmployeeActiveCommandHandler(IEmployeeRepository employees)
    {
        _employees = employees;
    }

    public async Task<Result<bool>> Handle(ToggleEmployeeActiveCommand request, CancellationToken cancellationToken)
    {
        var employee = await _employees.GetByIdAsync(request.Id, cancellationToken);
        if (employee is null)
            return Result.Failure<bool>(Error.NotFound("Employee", request.Id));

        if (employee.IsActive)
            employee.Deactivate();
        else
            employee.Reactivate();

        await _employees.UpdateAsync(employee, cancellationToken);
        return Result.Success(employee.IsActive);
    }
}

public sealed record DeleteEmployeeCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteEmployeeCommandHandler : IRequestHandler<DeleteEmployeeCommand, Result>
{
    private readonly IEmployeeRepository _employees;

    public DeleteEmployeeCommandHandler(IEmployeeRepository employees)
    {
        _employees = employees;
    }

    public async Task<Result> Handle(DeleteEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await _employees.GetByIdAsync(request.Id, cancellationToken);
        if (employee is null)
            return Result.Failure(Error.NotFound("Employee", request.Id));

        await _employees.DeleteAsync(employee, cancellationToken);
        return Result.Success();
    }
}
