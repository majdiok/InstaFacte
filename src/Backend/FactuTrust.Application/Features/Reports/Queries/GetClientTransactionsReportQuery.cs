using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Query to get client transactions report (invoices issued + payments received) for a date range.
/// </summary>
public sealed record GetClientTransactionsReportQuery(DateTime FromDate, DateTime ToDate)
    : IRequest<Result<IReadOnlyList<ClientTransactionReportRowDto>>>;

/// <summary>
/// Handler for GetClientTransactionsReportQuery.
/// </summary>
public sealed class GetClientTransactionsReportQueryHandler
    : IRequestHandler<GetClientTransactionsReportQuery, Result<IReadOnlyList<ClientTransactionReportRowDto>>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IPaymentRepository _paymentRepository;

    public GetClientTransactionsReportQueryHandler(
        IInvoiceRepository invoiceRepository,
        IPaymentRepository paymentRepository)
    {
        _invoiceRepository = invoiceRepository;
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<IReadOnlyList<ClientTransactionReportRowDto>>> Handle(
        GetClientTransactionsReportQuery request,
        CancellationToken cancellationToken)
    {
        var from = request.FromDate.Date;
        var to = request.ToDate.Date;

        var invoices = await _invoiceRepository.GetByDateRangeAsync(from, to, cancellationToken);
        var payments = await _paymentRepository.GetByDateRangeAsync(from, to, cancellationToken);

        var rows = new List<ClientTransactionReportRowDto>();

        foreach (var inv in invoices)
        {
            rows.Add(new ClientTransactionReportRowDto
            {
                Date = inv.IssueDate,
                TransactionType = "Facture",
                ClientName = inv.Client?.Name ?? "",
                Reference = inv.Number?.Value ?? "",
                Amount = inv.TotalAmount.Amount,
                Currency = inv.TotalAmount.Currency,
                InvoiceId = inv.Id,
                PaymentId = null
            });
        }

        foreach (var p in payments)
        {
            rows.Add(new ClientTransactionReportRowDto
            {
                Date = p.PaymentDate,
                TransactionType = "Paiement",
                ClientName = p.Invoice?.Client?.Name ?? "",
                Reference = p.Invoice?.Number?.Value ?? p.Reference ?? "",
                Amount = p.Amount.Amount,
                Currency = p.Amount.Currency,
                InvoiceId = p.InvoiceId,
                PaymentId = p.Id
            });
        }

        var ordered = rows.OrderByDescending(r => r.Date).ThenBy(r => r.TransactionType).ToList();
        return Result.Success<IReadOnlyList<ClientTransactionReportRowDto>>(ordered);
    }
}
