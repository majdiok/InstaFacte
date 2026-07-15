using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Quotes.Queries;

/// <summary>
/// Query to get aggregated totals for the quote list over the ENTIRE filtered set.
/// Mirrors <see cref="GetQuotesQuery"/> filters (without pagination) so the UI totals zone
/// reflects exactly the active filters, not just the current page.
/// </summary>
public sealed record GetQuotesSummaryQuery(
    string? SearchTerm = null,
    QuoteStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    Guid? ClientId = null) : IRequest<QuoteListSummaryDto>;

/// <summary>
/// Handler for GetQuotesSummaryQuery.
/// </summary>
public sealed class GetQuotesSummaryQueryHandler
    : IRequestHandler<GetQuotesSummaryQuery, QuoteListSummaryDto>
{
    private readonly IQuoteRepository _quoteRepository;

    public GetQuotesSummaryQueryHandler(IQuoteRepository quoteRepository)
    {
        _quoteRepository = quoteRepository;
    }

    public Task<QuoteListSummaryDto> Handle(GetQuotesSummaryQuery request, CancellationToken cancellationToken)
        => _quoteRepository.GetSummaryAsync(
            request.SearchTerm,
            request.Status,
            request.FromDate,
            request.ToDate,
            request.ClientId,
            cancellationToken);
}
