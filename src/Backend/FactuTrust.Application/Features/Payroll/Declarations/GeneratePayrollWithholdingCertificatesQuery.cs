using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Declarations;

/// <summary>
/// Prévisualisation des certificats de retenue à la source salariale pour un exercice.
/// </summary>
public sealed record GeneratePayrollWithholdingCertificatesQuery(int Year)
    : IRequest<Result<PayrollWithholdingCertificateBatchDto>>;

public sealed class GeneratePayrollWithholdingCertificatesQueryHandler
    : IRequestHandler<GeneratePayrollWithholdingCertificatesQuery, Result<PayrollWithholdingCertificateBatchDto>>
{
    private readonly PayrollWithholdingCertificateDataLoader _loader;

    public GeneratePayrollWithholdingCertificatesQueryHandler(
        IPayrollRunRepository runs,
        IEmployeeRepository employees,
        ITenantCompanySummaryProvider companySummary)
    {
        _loader = new PayrollWithholdingCertificateDataLoader(runs, employees, companySummary);
    }

    public Task<Result<PayrollWithholdingCertificateBatchDto>> Handle(
        GeneratePayrollWithholdingCertificatesQuery request,
        CancellationToken cancellationToken) =>
        _loader.LoadAsync(request.Year, cancellationToken);
}
