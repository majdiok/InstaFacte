using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.DeliveryNotes.Queries;

/// <summary>
/// Query to get aggregated totals for the delivery note list over the ENTIRE filtered set.
/// Mirrors <see cref="GetDeliveryNotesListQuery"/> filters (without pagination) so the UI totals
/// zone reflects exactly the active filters, not just the current page.
/// </summary>
public sealed record GetDeliveryNotesSummaryQuery(
    Guid? ClientId = null,
    DeliveryNoteStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Search = null) : IRequest<Result<DeliveryNoteListSummaryDto>>;

/// <summary>
/// Handler for GetDeliveryNotesSummaryQuery.
/// </summary>
public sealed class GetDeliveryNotesSummaryQueryHandler
    : IRequestHandler<GetDeliveryNotesSummaryQuery, Result<DeliveryNoteListSummaryDto>>
{
    private readonly IDeliveryNoteRepository _deliveryNoteRepository;

    public GetDeliveryNotesSummaryQueryHandler(IDeliveryNoteRepository deliveryNoteRepository)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
    }

    public async Task<Result<DeliveryNoteListSummaryDto>> Handle(GetDeliveryNotesSummaryQuery request, CancellationToken cancellationToken)
    {
        var summary = await _deliveryNoteRepository.GetSummaryAsync(
            request.ClientId,
            request.Status,
            request.FromDate,
            request.ToDate,
            request.Search,
            cancellationToken);

        return Result.Success(summary);
    }
}
