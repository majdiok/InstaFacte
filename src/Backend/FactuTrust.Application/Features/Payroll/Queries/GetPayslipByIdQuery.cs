using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Queries;

public sealed record GetPayslipByIdQuery(Guid Id) : IRequest<Result<PayslipDetailDto>>;

public sealed class GetPayslipByIdQueryHandler : IRequestHandler<GetPayslipByIdQuery, Result<PayslipDetailDto>>
{
    private readonly IPayrollRunRepository _runs;

    public GetPayslipByIdQueryHandler(IPayrollRunRepository runs)
    {
        _runs = runs;
    }

    public async Task<Result<PayslipDetailDto>> Handle(GetPayslipByIdQuery request, CancellationToken cancellationToken)
    {
        var payslip = await _runs.GetPayslipByIdAsync(request.Id, cancellationToken);
        if (payslip is null)
            return Result.Failure<PayslipDetailDto>(Error.NotFound("Payslip", request.Id));

        return Result.Success(PayrollMappings.ToPayslipDetailDto(payslip));
    }
}
