using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Query to get supplier payments (décaissements) report for a date range.
/// </summary>
public sealed record GetSupplierPaymentsReportQuery(DateTime FromDate, DateTime ToDate)
    : IRequest<Result<IReadOnlyList<SupplierPaymentReportRowDto>>>;

/// <summary>
/// Handler for GetSupplierPaymentsReportQuery.
/// </summary>
public sealed class GetSupplierPaymentsReportQueryHandler
    : IRequestHandler<GetSupplierPaymentsReportQuery, Result<IReadOnlyList<SupplierPaymentReportRowDto>>>
{
    private readonly ISupplierPaymentRepository _repository;

    public GetSupplierPaymentsReportQueryHandler(ISupplierPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<SupplierPaymentReportRowDto>>> Handle(
        GetSupplierPaymentsReportQuery request,
        CancellationToken cancellationToken)
    {
        var payments = await _repository.GetByDateRangeAsync(
            request.FromDate,
            request.ToDate,
            cancellationToken);

        var rows = payments.Select(p => new SupplierPaymentReportRowDto
        {
            PaymentId = p.Id,
            PaymentDate = p.PaymentDate,
            SupplierName = p.SupplierInvoice?.Supplier?.Name ?? "",
            SupplierInvoiceId = p.SupplierInvoiceId,
            InvoiceNumber = p.SupplierInvoice?.InvoiceNumber ?? "",
            Amount = p.Amount.Amount,
            Currency = p.Amount.Currency,
            MethodDisplay = p.Method.ToDisplayString(),
            Reference = p.Reference
        }).ToList();

        return Result.Success<IReadOnlyList<SupplierPaymentReportRowDto>>(rows);
    }
}
