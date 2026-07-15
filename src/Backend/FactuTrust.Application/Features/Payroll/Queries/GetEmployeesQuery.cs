using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
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

    public GetEmployeesQueryHandler(IEmployeeRepository employees)
    {
        _employees = employees;
    }

    public async Task<PagedResult<EmployeeListDto>> Handle(GetEmployeesQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 20 : request.PageSize;

        var (items, total) = await _employees.SearchAsync(request.Search, request.IsActive, page, pageSize, cancellationToken);

        // Load contracts for the current page to expose the current base salary / job title.
        var dtos = new List<EmployeeListDto>(items.Count);
        foreach (var e in items)
        {
            var withContracts = await _employees.GetByIdWithContractsAsync(e.Id, cancellationToken) ?? e;
            dtos.Add(PayrollMappings.ToListDto(withContracts));
        }

        return PagedResult<EmployeeListDto>.Create(dtos, page, pageSize, total);
    }
}
