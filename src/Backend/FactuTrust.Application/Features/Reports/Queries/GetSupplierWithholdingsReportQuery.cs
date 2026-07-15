using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

public sealed record GetSupplierWithholdingsReportQuery(DateTime FromDate, DateTime ToDate)
    : IRequest<Result<IReadOnlyList<SupplierWithholdingReportRowDto>>>;

public sealed class GetSupplierWithholdingsReportQueryHandler
    : IRequestHandler<GetSupplierWithholdingsReportQuery, Result<IReadOnlyList<SupplierWithholdingReportRowDto>>>
{
    private readonly ISupplierInvoiceRepository _invoiceRepository;

    public GetSupplierWithholdingsReportQueryHandler(ISupplierInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<Result<IReadOnlyList<SupplierWithholdingReportRowDto>>> Handle(
        GetSupplierWithholdingsReportQuery request,
        CancellationToken cancellationToken)
    {
        var rows = await _invoiceRepository.GetSupplierWithholdingReportRowsAsync(
            request.FromDate,
            request.ToDate,
            cancellationToken);

        return Result.Success<IReadOnlyList<SupplierWithholdingReportRowDto>>(rows);
    }
}