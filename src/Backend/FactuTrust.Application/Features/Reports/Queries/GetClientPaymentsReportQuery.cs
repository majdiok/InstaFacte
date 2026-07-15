using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Query to get client payments (encaissements) report for a date range.
/// </summary>
public sealed record GetClientPaymentsReportQuery(DateTime FromDate, DateTime ToDate)
    : IRequest<Result<IReadOnlyList<ClientPaymentReportRowDto>>>;

/// <summary>
/// Handler for GetClientPaymentsReportQuery.
/// </summary>
public sealed class GetClientPaymentsReportQueryHandler
    : IRequestHandler<GetClientPaymentsReportQuery, Result<IReadOnlyList<ClientPaymentReportRowDto>>>
{
    private readonly IPaymentRepository _paymentRepository;

    public GetClientPaymentsReportQueryHandler(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<IReadOnlyList<ClientPaymentReportRowDto>>> Handle(
        GetClientPaymentsReportQuery request,
        CancellationToken cancellationToken)
    {
        var sources = await _paymentRepository.GetClientPaymentReportSourcesAsync(
            request.FromDate,
            request.ToDate,
            cancellationToken);

        var rows = sources.Select(p => new ClientPaymentReportRowDto
        {
            PaymentId = p.PaymentId,
            PaymentDate = p.PaymentDate,
            ClientName = p.ClientName,
            InvoiceId = p.InvoiceId,
            InvoiceNumber = p.InvoiceNumber,
            Amount = p.Amount,
            Currency = p.Currency,
            MethodDisplay = ((PaymentMethod)p.Method).ToDisplayString(),
            Reference = p.Reference
        }).ToList();

        return Result.Success<IReadOnlyList<ClientPaymentReportRowDto>>(rows);
    }
}
