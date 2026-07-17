using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Queries;

public sealed record GetPayslipPdfQuery(Guid PayslipId) : IRequest<Result<byte[]>>;

public sealed class GetPayslipPdfQueryHandler : IRequestHandler<GetPayslipPdfQuery, Result<byte[]>>
{
    private readonly IPayrollRunRepository _runs;
    private readonly IEmployeeRepository _employees;
    private readonly ILeaveBalanceAccrualRepository _accruals;
    private readonly ILeaveRequestRepository _leaves;
    private readonly IPdfService _pdf;
    private readonly ITenantCompanySummaryProvider _companySummary;

    public GetPayslipPdfQueryHandler(
        IPayrollRunRepository runs,
        IEmployeeRepository employees,
        ILeaveBalanceAccrualRepository accruals,
        ILeaveRequestRepository leaves,
        IPdfService pdf,
        ITenantCompanySummaryProvider companySummary)
    {
        _runs = runs;
        _employees = employees;
        _accruals = accruals;
        _leaves = leaves;
        _pdf = pdf;
        _companySummary = companySummary;
    }

    public async Task<Result<byte[]>> Handle(GetPayslipPdfQuery request, CancellationToken cancellationToken)
    {
        var payslip = await _runs.GetPayslipByIdAsync(request.PayslipId, cancellationToken);
        if (payslip is null)
            return Result.Failure<byte[]>(Error.NotFound("Payslip", request.PayslipId));

        var dto = PayrollMappings.ToPayslipDetailDto(payslip);

        var employee = await _employees.GetByIdWithContractsAsync(payslip.EmployeeId, cancellationToken);
        var company = await _companySummary.GetCurrentTenantSummaryAsync(cancellationToken);

        dto = await PayslipDetailEnrichment.EnrichAsync(
            dto,
            employee,
            payslip.Year,
            payslip.Month,
            _accruals,
            _leaves,
            company?.AddressLine,
            cancellationToken);

        var bytes = await _pdf.GeneratePayslipPdfAsync(dto, company?.CompanyName ?? string.Empty, cancellationToken);

        if (bytes.Length == 0)
            return Result.Failure<byte[]>(Error.Validation("Pdf", "Le PDF généré est vide."));

        return Result.Success(bytes);
    }
}
