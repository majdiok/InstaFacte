using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

public sealed record SearchJournalEntriesQuery(
    string? AccountNumber,
    string? JournalCode,
    DateTime? From,
    DateTime? To,
    decimal? MinAmount,
    decimal? MaxAmount,
    string? Label,
    string? LetteringCode,
    int? Status,
    int Take,
    string? PieceRef = null,
    int? EntryNumber = null) : IRequest<Result<IReadOnlyList<JournalSearchRowDto>>>;

public sealed class SearchJournalEntriesQueryHandler
    : IRequestHandler<SearchJournalEntriesQuery, Result<IReadOnlyList<JournalSearchRowDto>>>
{
    private readonly IAccountingReportingService _reporting;

    public SearchJournalEntriesQueryHandler(IAccountingReportingService reporting)
    {
        _reporting = reporting;
    }

    public Task<Result<IReadOnlyList<JournalSearchRowDto>>> Handle(SearchJournalEntriesQuery request, CancellationToken cancellationToken)
        => _reporting.SearchJournalEntriesAsync(
            request.AccountNumber, request.JournalCode, request.From, request.To,
            request.MinAmount, request.MaxAmount, request.Label, request.LetteringCode, request.Status, request.Take,
            request.PieceRef, request.EntryNumber,
            cancellationToken);
}
