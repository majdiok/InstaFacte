using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Query to get sales VAT report (TVA ventes). <paramref name="RealizedOnly"/> = true exclut les
/// factures Brouillon/Annulée (assiette légale de la déclaration mensuelle) ; false (défaut) =
/// comportement historique des états de consultation.
/// </summary>
public sealed record GetSalesVatReportQuery(DateTime FromDate, DateTime ToDate, bool RealizedOnly = false)
    : IRequest<Result<IReadOnlyList<SalesVatReportRowDto>>>;

/// <summary>
/// Handler for GetSalesVatReportQuery.
/// </summary>
public sealed class GetSalesVatReportQueryHandler
    : IRequestHandler<GetSalesVatReportQuery, Result<IReadOnlyList<SalesVatReportRowDto>>>
{
    private readonly IInvoiceRepository _invoiceRepository;

    public GetSalesVatReportQueryHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<Result<IReadOnlyList<SalesVatReportRowDto>>> Handle(
        GetSalesVatReportQuery request,
        CancellationToken cancellationToken)
    {
        var from = request.FromDate.Date;
        var to = request.ToDate.Date;
        var rows = await _invoiceRepository.GetSalesVatAggregatedAsync(from, to, request.RealizedOnly, cancellationToken);
        return Result.Success<IReadOnlyList<SalesVatReportRowDto>>(rows);
    }
}