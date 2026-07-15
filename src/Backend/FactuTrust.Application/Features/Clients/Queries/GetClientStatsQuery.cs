using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Clients.Queries;

/// <summary>
/// Query to get client statistics (invoices, quotes, revenue).
/// </summary>
public sealed record GetClientStatsQuery(Guid ClientId) : IRequest<Result<ClientStatsDto>>;

/// <summary>
/// Handler for GetClientStatsQuery.
/// </summary>
public sealed class GetClientStatsQueryHandler : IRequestHandler<GetClientStatsQuery, Result<ClientStatsDto>>
{
    private readonly IClientRepository _clientRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IQuoteRepository _quoteRepository;

    public GetClientStatsQueryHandler(
        IClientRepository clientRepository,
        IInvoiceRepository invoiceRepository,
        IQuoteRepository quoteRepository)
    {
        _clientRepository = clientRepository;
        _invoiceRepository = invoiceRepository;
        _quoteRepository = quoteRepository;
    }

    public async Task<Result<ClientStatsDto>> Handle(GetClientStatsQuery request, CancellationToken cancellationToken)
    {
        if (!await _clientRepository.ExistsAsync(request.ClientId, cancellationToken))
            return Result.Failure<ClientStatsDto>(Error.NotFound("Client", request.ClientId));

        var invoices = await _invoiceRepository.GetByClientIdAsync(request.ClientId, cancellationToken);
        var quotes = await _quoteRepository.GetByClientIdAsync(request.ClientId, cancellationToken);

        var nonCancelled = invoices.Where(i => i.Status != InvoiceStatus.Cancelled).ToList();
        var totalRevenue = nonCancelled.Sum(i => i.TotalAmount.Amount);
        var paidCount = invoices.Count(i => i.Status == InvoiceStatus.Paid);
        var overdueCount = invoices.Count(i => i.Status == InvoiceStatus.Overdue);
        var pendingCount = invoices.Count(i => i.Status is InvoiceStatus.PartiallyPaid or InvoiceStatus.Overdue
            or InvoiceStatus.Draft or InvoiceStatus.Validated or InvoiceStatus.Signed);

        var lastInvoice = invoices.OrderByDescending(i => i.IssueDate).FirstOrDefault();
        var lastQuote = quotes.OrderByDescending(q => q.IssueDate).FirstOrDefault();

        var dto = new ClientStatsDto
        {
            TotalInvoices = invoices.Count,
            PaidInvoices = paidCount,
            PendingInvoices = pendingCount,
            OverdueInvoices = overdueCount,
            TotalRevenue = totalRevenue,
            AverageInvoiceAmount = nonCancelled.Count > 0 ? totalRevenue / nonCancelled.Count : 0,
            LastInvoiceDate = lastInvoice?.IssueDate,
            TotalQuotes = quotes.Count,
            LastQuoteDate = lastQuote?.IssueDate
        };

        return Result.Success(dto);
    }
}
