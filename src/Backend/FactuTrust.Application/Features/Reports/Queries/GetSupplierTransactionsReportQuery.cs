using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Query to get supplier transactions report (invoices received + payments made) for a date range.
/// </summary>
public sealed record GetSupplierTransactionsReportQuery(DateTime FromDate, DateTime ToDate)
    : IRequest<Result<IReadOnlyList<SupplierTransactionReportRowDto>>>;

/// <summary>
/// Handler for GetSupplierTransactionsReportQuery.
/// </summary>
public sealed class GetSupplierTransactionsReportQueryHandler
    : IRequestHandler<GetSupplierTransactionsReportQuery, Result<IReadOnlyList<SupplierTransactionReportRowDto>>>
{
    private readonly ISupplierInvoiceRepository _invoiceRepository;
    private readonly ISupplierPaymentRepository _paymentRepository;

    public GetSupplierTransactionsReportQueryHandler(
        ISupplierInvoiceRepository invoiceRepository,
        ISupplierPaymentRepository paymentRepository)
    {
        _invoiceRepository = invoiceRepository;
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<IReadOnlyList<SupplierTransactionReportRowDto>>> Handle(
        GetSupplierTransactionsReportQuery request,
        CancellationToken cancellationToken)
    {
        var from = request.FromDate.Date;
        var to = request.ToDate.Date;

        var (invoices, _) = await _invoiceRepository.SearchAsync(
            null, null, null, null, null, from, to, 1, 10_000, false, cancellationToken);
        var payments = await _paymentRepository.GetByDateRangeAsync(from, to, cancellationToken);

        var rows = new List<SupplierTransactionReportRowDto>();

        foreach (var inv in invoices)
        {
            rows.Add(new SupplierTransactionReportRowDto
            {
                Date = inv.InvoiceDate,
                TransactionType = "Facture fournisseur",
                SupplierName = inv.Supplier?.Name ?? "",
                Reference = inv.InvoiceNumber ?? "",
                Amount = inv.TotalAmount.Amount,
                Currency = inv.TotalAmount.Currency,
                SupplierInvoiceId = inv.Id,
                PaymentId = null
            });
        }

        foreach (var p in payments)
        {
            rows.Add(new SupplierTransactionReportRowDto
            {
                Date = p.PaymentDate,
                TransactionType = "Paiement",
                SupplierName = p.SupplierInvoice?.Supplier?.Name ?? "",
                Reference = p.SupplierInvoice?.InvoiceNumber ?? p.Reference ?? "",
                Amount = p.Amount.Amount,
                Currency = p.Amount.Currency,
                SupplierInvoiceId = p.SupplierInvoiceId,
                PaymentId = p.Id
            });
        }

        var ordered = rows.OrderByDescending(r => r.Date).ThenBy(r => r.TransactionType).ToList();
        return Result.Success<IReadOnlyList<SupplierTransactionReportRowDto>>(ordered);
    }
}
