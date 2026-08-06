using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Services.Payroll;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Queries;

public sealed record GetEmployeesQuery(
    string? Search,
    bool? IsActive,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<EmployeeListDto>>;

public sealed class GetEmployeesQueryHandler : IRequestHandler<GetEmployeesQuery, PagedResult<EmployeeListDto>>
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmployeeDependentParentRepository _dependentParents;

    public GetEmployeesQueryHandler(
        IEmployeeRepository employees,
        IEmployeeDependentParentRepository dependentParents)
    {
        _employees = employees;
        _dependentParents = dependentParents;
    }

    public async Task<PagedResult<EmployeeListDto>> Handle(GetEmployeesQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 20 : request.PageSize;

        var (items, total) = await _employees.SearchAsync(request.Search, request.IsActive, page, pageSize, cancellationToken);
        var ids = items.Select(e => e.Id).ToList();
        var claims = await _dependentParents.GetActiveByEmployeeIdsAsync(ids, cancellationToken);
        var claimsByEmployee = claims.GroupBy(c => c.EmployeeId).ToDictionary(g => g.Key, g => (IReadOnlyList<Domain.Entities.Payroll.EmployeeDependentParent>)g.ToList());
        var cinIndex = await _dependentParents.GetActiveCinIndexAsync(cancellationToken);

        var dtos = new List<EmployeeListDto>(items.Count);
        foreach (var e in items)
        {
            var withContracts = await _employees.GetByIdWithContractsAsync(e.Id, cancellationToken) ?? e;
            var employeeClaims = claimsByEmployee.GetValueOrDefault(e.Id) ?? Array.Empty<Domain.Entities.Payroll.EmployeeDependentParent>();
            var eligibility = ParentDeductionEligibilityResolver.ResolveForEmployee(
                withContracts.DependentParents,
                employeeClaims,
                cinIndex,
                e.Id);
            dtos.Add(PayrollMappings.ToListDto(
                withContracts,
                ParentDeductionEligibilityResolver.ResolveStatusLabel(eligibility.Status)));
        }

        return PagedResult<EmployeeListDto>.Create(dtos, page, pageSize, total);
    }
}
