using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Services.Payroll;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Queries;

public sealed record GetEmployeeByIdQuery(Guid Id) : IRequest<Result<EmployeeDetailDto>>;

public sealed class GetEmployeeByIdQueryHandler : IRequestHandler<GetEmployeeByIdQuery, Result<EmployeeDetailDto>>
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeDependentParentRepository _dependentParents;

    public GetEmployeeByIdQueryHandler(
        IEmployeeRepository employees,
        IEmployeeDependentParentRepository dependentParents)
    {
        _employees = employees;
        _dependentParents = dependentParents;
    }

    public async Task<Result<EmployeeDetailDto>> Handle(GetEmployeeByIdQuery request, CancellationToken cancellationToken)
    {
        var employee = await _employees.GetByIdWithContractsAsync(request.Id, cancellationToken);
        if (employee is null)
            return Result.Failure<EmployeeDetailDto>(Error.NotFound("Employee", request.Id));

        var claims = await _dependentParents.GetActiveByEmployeeIdAsync(request.Id, cancellationToken);
        var cinIndex = await _dependentParents.GetActiveCinIndexAsync(cancellationToken);
        var eligibility = ParentDeductionEligibilityResolver.ResolveForEmployee(
            employee.DependentParents,
            claims,
            cinIndex,
            employee.Id);

        var dto = PayrollMappings.ToDetailDto(employee, claims) with
        {
            ParentClaimsStatus = ParentDeductionEligibilityResolver.ResolveStatusLabel(eligibility.Status)
        };
        return Result.Success(dto);
    }
}
