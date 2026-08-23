using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.SalesReturnNotes.Queries;

public sealed record GetSalesReturnNotePrefillFromDeliveryNoteQuery(Guid DeliveryNoteId)
    : IRequest<Result<SalesReturnNotePrefillDto>>;

public sealed class GetSalesReturnNotePrefillFromDeliveryNoteQueryHandler
    : IRequestHandler<GetSalesReturnNotePrefillFromDeliveryNoteQuery, Result<SalesReturnNotePrefillDto>>
{
    private readonly IDeliveryNoteRepository _deliveryNoteRepository;

    public GetSalesReturnNotePrefillFromDeliveryNoteQueryHandler(IDeliveryNoteRepository deliveryNoteRepository)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
    }

    public async Task<Result<SalesReturnNotePrefillDto>> Handle(
        GetSalesReturnNotePrefillFromDeliveryNoteQuery request,
        CancellationToken cancellationToken)
    {
        var deliveryNote = await _deliveryNoteRepository.GetByIdWithDetailsAsync(
            request.DeliveryNoteId, cancellationToken);
        if (deliveryNote is null)
            return Result.Failure<SalesReturnNotePrefillDto>(Error.NotFound("DeliveryNote", request.DeliveryNoteId));

        if (deliveryNote.InvoiceId.HasValue)
            return Result.Failure<SalesReturnNotePrefillDto>(Error.Validation("DeliveryNote",
                "Ce bon de livraison a déjà été facturé — utilisez un avoir"));

        if (!deliveryNote.Status.CanBeInvoiced())
            return Result.Failure<SalesReturnNotePrefillDto>(Error.Validation("DeliveryNote",
                $"Un bon de retour n'est possible que sur un bon livré et non facturé (statut : {deliveryNote.Status.ToDisplayString()})"));

        if (!deliveryNote.HasInvoiceableQuantity)
            return Result.Failure<SalesReturnNotePrefillDto>(Error.Validation("DeliveryNote",
                "Toutes les quantités livrées ont déjà été retournées"));

        var dto = new SalesReturnNotePrefillDto
        {
            DeliveryNoteId = deliveryNote.Id,
            DeliveryNoteNumber = deliveryNote.Number.Value,
            ClientId = deliveryNote.ClientId,
            ClientName = deliveryNote.Client?.Name ?? "Client inconnu",
            WarehouseId = deliveryNote.WarehouseId,
            WarehouseName = deliveryNote.Warehouse?.Name,
            Lines = deliveryNote.Lines
                .Where(l => l.InvoiceableQuantity > 0)
                .OrderBy(l => l.LineNumber)
                .Select(l => new SalesReturnNotePrefillLineDto
                {
                    DeliveryNoteLineId = l.Id,
                    LineNumber = l.LineNumber,
                    ProductId = l.ProductId,
                    ProductCode = l.ProductCode,
                    Designation = l.Designation,
                    Unit = l.Unit,
                    UnitPriceHT = l.UnitPriceHT,
                    DeliveredQuantity = l.DeliveredQuantity,
                    AlreadyReturnedQuantity = l.ReturnedQuantity,
                    InvoiceableQuantity = l.InvoiceableQuantity
                })
                .ToList()
        };

        return Result.Success(dto);
    }
}
