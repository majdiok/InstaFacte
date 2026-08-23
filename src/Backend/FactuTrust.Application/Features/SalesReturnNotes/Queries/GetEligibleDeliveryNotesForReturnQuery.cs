using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.SalesReturnNotes.Queries;

public sealed record GetEligibleDeliveryNotesForReturnQuery(
    Guid? ClientId = null,
    string? Search = null) : IRequest<IReadOnlyList<EligibleDeliveryNoteDto>>;

public sealed class GetEligibleDeliveryNotesForReturnQueryHandler
    : IRequestHandler<GetEligibleDeliveryNotesForReturnQuery, IReadOnlyList<EligibleDeliveryNoteDto>>
{
    private readonly IDeliveryNoteRepository _deliveryNoteRepository;

    public GetEligibleDeliveryNotesForReturnQueryHandler(IDeliveryNoteRepository deliveryNoteRepository)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
    }

    public async Task<IReadOnlyList<EligibleDeliveryNoteDto>> Handle(
        GetEligibleDeliveryNotesForReturnQuery request,
        CancellationToken cancellationToken)
    {
        var notes = await _deliveryNoteRepository.GetEligibleForReturnAsync(
            request.ClientId, request.Search, cancellationToken);

        return notes.Select(n => new EligibleDeliveryNoteDto
        {
            Id = n.Id,
            Number = n.Number.Value,
            IssueDate = n.IssueDate,
            ClientId = n.ClientId,
            ClientName = n.Client?.Name ?? "Client inconnu",
            StatusDisplay = n.Status.ToDisplayString(),
            TotalInvoiceableQuantity = n.TotalInvoiceableQuantity,
            InvoiceableLineCount = n.Lines.Count(l => l.InvoiceableQuantity > 0),
            WarehouseId = n.WarehouseId,
            WarehouseName = n.Warehouse?.Name
        }).ToList();
    }
}
