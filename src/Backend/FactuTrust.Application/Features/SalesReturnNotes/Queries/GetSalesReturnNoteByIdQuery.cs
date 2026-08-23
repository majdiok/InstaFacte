using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.SalesReturnNotes.Queries;

public sealed record GetSalesReturnNoteByIdQuery(Guid Id) : IRequest<Result<SalesReturnNoteDetailDto>>;

public sealed class GetSalesReturnNoteByIdQueryHandler
    : IRequestHandler<GetSalesReturnNoteByIdQuery, Result<SalesReturnNoteDetailDto>>
{
    private readonly ISalesReturnNoteRepository _repository;

    public GetSalesReturnNoteByIdQueryHandler(ISalesReturnNoteRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<SalesReturnNoteDetailDto>> Handle(
        GetSalesReturnNoteByIdQuery request,
        CancellationToken cancellationToken)
    {
        var note = await _repository.GetByIdWithDetailsAsync(request.Id, cancellationToken);
        if (note is null)
            return Result.Failure<SalesReturnNoteDetailDto>(Error.NotFound("SalesReturnNote", request.Id));

        return Result.Success(Map(note));
    }

    internal static SalesReturnNoteDetailDto Map(Domain.Entities.SalesReturnNote note) => new()
    {
        Id = note.Id,
        Number = note.Number.Value,
        ReturnDate = note.ReturnDate,
        Status = note.Status,
        StatusDisplay = note.Status.ToDisplayString(),
        StatusCss = note.Status.ToCssClass(),
        ClientId = note.ClientId,
        ClientName = note.Client?.Name ?? "Client inconnu",
        DeliveryNoteId = note.DeliveryNoteId,
        DeliveryNoteNumber = note.DeliveryNote?.Number.Value ?? "—",
        WarehouseId = note.WarehouseId,
        WarehouseName = note.Warehouse?.Name,
        Reason = note.Reason,
        Notes = note.Notes,
        ConfirmedAt = note.ConfirmedAt,
        TotalHT = note.TotalHT,
        TotalFodec = note.TotalFodec,
        TotalVAT = note.TotalVAT,
        TotalTTC = note.TotalTTC,
        TotalReturnedQuantity = note.TotalReturnedQuantity,
        Lines = note.Lines.OrderBy(l => l.LineNumber).Select(l => new SalesReturnNoteLineDto
        {
            Id = l.Id,
            DeliveryNoteLineId = l.DeliveryNoteLineId,
            LineNumber = l.LineNumber,
            ProductId = l.ProductId,
            ProductCode = l.ProductCode,
            Designation = l.Designation,
            Description = l.Description,
            Unit = l.Unit,
            UnitPriceHT = l.UnitPriceHT,
            VatRatePercent = l.VatRatePercent,
            DiscountPercent = l.DiscountPercent,
            IsFodecApplicable = l.IsFodecApplicable,
            FodecRatePercent = l.FodecRatePercent,
            ReturnedQuantity = l.ReturnedQuantity,
            Notes = l.Notes,
            TotalHT = l.TotalHT,
            FodecAmount = l.FodecAmount,
            TotalVAT = l.TotalVAT,
            TotalTTC = l.TotalTTC
        }).ToList(),
        CreatedAt = note.CreatedAt,
        UpdatedAt = note.UpdatedAt
    };
}
