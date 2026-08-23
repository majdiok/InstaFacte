using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.SalesReturnNotes.Queries;

public sealed record GetSalesReturnNotesSummaryQuery(
    string? Search = null,
    SalesReturnNoteStatus? Status = null,
    Guid? ClientId = null,
    Guid? DeliveryNoteId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null) : IRequest<SalesReturnNoteListSummaryDto>;

public sealed class GetSalesReturnNotesSummaryQueryHandler
    : IRequestHandler<GetSalesReturnNotesSummaryQuery, SalesReturnNoteListSummaryDto>
{
    private readonly ISalesReturnNoteRepository _repository;

    public GetSalesReturnNotesSummaryQueryHandler(ISalesReturnNoteRepository repository)
    {
        _repository = repository;
    }

    public Task<SalesReturnNoteListSummaryDto> Handle(
        GetSalesReturnNotesSummaryQuery request,
        CancellationToken cancellationToken)
        => _repository.GetSummaryAsync(
            request.Search,
            request.Status,
            request.ClientId,
            request.DeliveryNoteId,
            request.FromDate,
            request.ToDate,
            cancellationToken);
}
