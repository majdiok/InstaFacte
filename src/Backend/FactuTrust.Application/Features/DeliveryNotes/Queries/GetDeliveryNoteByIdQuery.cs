using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.DeliveryNotes.Queries;

/// <summary>
/// Query to get a delivery note by ID.
/// </summary>
public sealed record GetDeliveryNoteByIdQuery(Guid DeliveryNoteId) : IRequest<Result<DeliveryNoteDetailDto>>;

/// <summary>
/// Handler for GetDeliveryNoteByIdQuery.
/// </summary>
public sealed class GetDeliveryNoteByIdQueryHandler : IRequestHandler<GetDeliveryNoteByIdQuery, Result<DeliveryNoteDetailDto>>
{
    private readonly IDeliveryNoteRepository _deliveryNoteRepository;
    private readonly ISalesReturnNoteRepository _salesReturnNoteRepository;

    public GetDeliveryNoteByIdQueryHandler(
        IDeliveryNoteRepository deliveryNoteRepository,
        ISalesReturnNoteRepository salesReturnNoteRepository)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
        _salesReturnNoteRepository = salesReturnNoteRepository;
    }

    public async Task<Result<DeliveryNoteDetailDto>> Handle(GetDeliveryNoteByIdQuery request, CancellationToken cancellationToken)
    {
        var deliveryNote = await _deliveryNoteRepository.GetByIdWithDetailsAsync(request.DeliveryNoteId, cancellationToken);
        if (deliveryNote is null)
            return Result.Failure<DeliveryNoteDetailDto>(Error.NotFound("DeliveryNote", request.DeliveryNoteId));

        var lineDtos = deliveryNote.Lines.Select(l => new DeliveryNoteLineDto(
            l.Id,
            l.LineNumber,
            l.ProductId,
            l.ProductCode,
            l.Designation,
            l.Description,
            l.Unit,
            l.UnitPriceHT,
            l.VatRatePercent,
            l.OrderedQuantity,
            l.DeliveredQuantity,
            l.RejectedQuantity,
            l.RejectionReason,
            l.PendingQuantity,
            l.TotalHT,
            l.TotalVAT,
            l.TotalTTC,
            l.IsFullyDelivered,
            l.Notes,
            l.DiscountPercent,
            l.AppliedPromotionId,
            l.AppliedPromotionName,
            l.DiscountAmount,
            l.IsFodecApplicable,
            l.FodecRatePercent,
            l.FodecAmount,
            l.ReturnedQuantity,
            l.InvoiceableQuantity)).ToList();

        var returnNotes = (await _salesReturnNoteRepository.GetByDeliveryNoteIdAsync(
                deliveryNote.Id, cancellationToken))
            .Select(n => new LinkedSalesReturnNoteDto(
                n.Id,
                n.Number.Value,
                n.Status,
                n.Status.ToDisplayString(),
                n.ReturnDate,
                n.TotalReturnedQuantity))
            .ToList();

        var dto = new DeliveryNoteDetailDto(
            deliveryNote.Id,
            deliveryNote.Number.Value,
            deliveryNote.IssueDate,
            deliveryNote.DeliveryDate,
            deliveryNote.Status,
            deliveryNote.Status.ToDisplayString(),
            deliveryNote.ClientId,
            deliveryNote.Client?.Name ?? "Client inconnu",
            deliveryNote.Client?.Email?.Value,
            deliveryNote.Reference,
            deliveryNote.Notes,
            deliveryNote.DeliveryAddress,
            deliveryNote.DeliveryCity,
            deliveryNote.DeliveryPostalCode,
            deliveryNote.RecipientName,
            deliveryNote.SignedAt,
            deliveryNote.FailureReason,
            deliveryNote.FailedAt,
            deliveryNote.CancellationReason,
            deliveryNote.CancelledAt,
            deliveryNote.AllowGroupInvoicing,
            deliveryNote.InvoiceId,
            deliveryNote.Invoice?.Number?.Value,
            deliveryNote.InvoicedAt,
            deliveryNote.TotalHT,
            deliveryNote.TotalVAT,
            deliveryNote.TotalTTC,
            lineDtos,
            deliveryNote.CreatedAt,
            deliveryNote.UpdatedAt,
            deliveryNote.WarehouseId,
            deliveryNote.Warehouse?.Name,
            deliveryNote.TotalReturnedQuantity,
            deliveryNote.TotalInvoiceableQuantity,
            deliveryNote.HasInvoiceableQuantity,
            returnNotes);

        return Result.Success(dto);
    }
}
