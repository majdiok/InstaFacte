using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.LeaveBalance;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Queries;

public sealed record GetPayslipByIdQuery(Guid Id) : IRequest<Result<PayslipDetailDto>>;

public sealed class GetPayslipByIdQueryHandler : IRequestHandler<GetPayslipByIdQuery, Result<PayslipDetailDto>>
{
    private readonly IPayrollRunRepository _runs;
    private readonly IEmployeeRepository _employees;
    private readonly ILeaveBalanceAccrualRepository _accruals;
    private readonly ILeaveRequestRepository _leaves;

    public GetPayslipByIdQueryHandler(
        IPayrollRunRepository runs,
        IEmployeeRepository employees,
        ILeaveBalanceAccrualRepository accruals,
        ILeaveRequestRepository leaves)
    {
        _runs = runs;
        _employees = employees;
        _accruals = accruals;
        _leaves = leaves;
    }

    public async Task<Result<PayslipDetailDto>> Handle(GetPayslipByIdQuery request, CancellationToken cancellationToken)
    {
        var payslip = await _runs.GetPayslipByIdAsync(request.Id, cancellationToken);
        if (payslip is null)
            return Result.Failure<PayslipDetailDto>(Error.NotFound("Payslip", request.Id));

        var dto = PayrollMappings.ToPayslipDetailDto(payslip);

        // Même enrichissement que le PDF : CIN, date d'embauche et solde de congés.
        var employee = await _employees.GetByIdAsync(payslip.EmployeeId, cancellationToken);
        if (employee is not null)
        {
            var balance = await GetEmployeeLeaveBalanceQueryHandler.BuildBalanceDtoAsync(
                employee, payslip.Year, _accruals, _leaves, cancellationToken);

            dto = dto with
            {
                Cin = employee.Cin,
                HireDate = employee.HireDate,
                LeaveBalanceRemaining = balance.Remaining
            };
        }

        return Result.Success(dto);
    }
}
