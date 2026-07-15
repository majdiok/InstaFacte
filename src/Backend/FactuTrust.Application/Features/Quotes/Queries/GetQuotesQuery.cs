using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Quotes.Queries;

/// <summary>
/// Query to get paginated quotes with optional filtering.
/// </summary>
public sealed record GetQuotesQuery(
    string? SearchTerm = null,
    QuoteStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    Guid? ClientId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<QuoteListDto>>;

/// <summary>
/// Handler for GetQuotesQuery.
/// </summary>
public sealed class GetQuotesQueryHandler : IRequestHandler<GetQuotesQuery, PagedResult<QuoteListDto>>
{
    private readonly IQuoteRepository _quoteRepository;

    public GetQuotesQueryHandler(IQuoteRepository quoteRepository)
    {
        _quoteRepository = quoteRepository;
    }

    public async Task<PagedResult<QuoteListDto>> Handle(GetQuotesQuery request, CancellationToken cancellationToken)
    {
        var (quotes, totalCount) = await _quoteRepository.SearchAsync(
            request.SearchTerm,
            request.Status,
            request.FromDate,
            request.ToDate,
            request.ClientId,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = quotes.Select(q => new QuoteListDto
        {
            Id = q.Id,
            Number = q.Number.Value,
            IssueDate = q.IssueDate,
            ExpiryDate = q.ExpiryDate,
            Status = q.Status.ToDisplayString(),
            StatusCssClass = q.Status.ToCssClass(),
            ClientName = q.Client.Name,
            TotalAmount = q.TotalAmount.Amount,
            Currency = q.TotalAmount.Currency,
            IsExpired = q.ExpiryDate < DateTime.UtcNow.Date && q.Status != QuoteStatus.Expired,
            IsConverted = q.ConvertedInvoiceId.HasValue,
            ConvertedInvoiceId = q.ConvertedInvoiceId
        }).ToList();

        return PagedResult<QuoteListDto>.Create(dtos, request.Page, request.PageSize, totalCount);
    }
}
