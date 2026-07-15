using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Queries;

public sealed record GetEmployeeByIdQuery(Guid Id) : IRequest<Result<EmployeeDetailDto>>;

public sealed class GetEmployeeByIdQueryHandler : IRequestHandler<GetEmployeeByIdQuery, Result<EmployeeDetailDto>>
{
    private readonly IEmployeeRepository _employees;

    public GetEmployeeByIdQueryHandler(IEmployeeRepository employees)
    {
        _employees = employees;
    }

    public async Task<Result<EmployeeDetailDto>> Handle(GetEmployeeByIdQuery request, CancellationToken cancellationToken)
    {
        var employee = await _employees.GetByIdWithContractsAsync(request.Id, cancellationToken);
        if (employee is null)
            return Result.Failure<EmployeeDetailDto>(Error.NotFound("Employee", request.Id));

        return Result.Success(PayrollMappings.ToDetailDto(employee));
    }
}
