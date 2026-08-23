using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.SalesReturnNotes.Queries;

public sealed record GetSalesReturnNotesQuery(
    string? Search = null,
    SalesReturnNoteStatus? Status = null,
    Guid? ClientId = null,
    Guid? DeliveryNoteId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<SalesReturnNoteListDto>>;

public sealed class GetSalesReturnNotesQueryHandler
    : IRequestHandler<GetSalesReturnNotesQuery, PagedResult<SalesReturnNoteListDto>>
{
    private readonly ISalesReturnNoteRepository _repository;

    public GetSalesReturnNotesQueryHandler(ISalesReturnNoteRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<SalesReturnNoteListDto>> Handle(
        GetSalesReturnNotesQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _repository.SearchAsync(
            request.Search,
            request.Status,
            request.ClientId,
            request.DeliveryNoteId,
            request.FromDate,
            request.ToDate,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(MapList).ToList();
        return PagedResult<SalesReturnNoteListDto>.Create(dtos, request.Page, request.PageSize, totalCount);
    }

    internal static SalesReturnNoteListDto MapList(Domain.Entities.SalesReturnNote n) => new()
    {
        Id = n.Id,
        Number = n.Number.Value,
        ReturnDate = n.ReturnDate,
        ClientId = n.ClientId,
        ClientName = n.Client?.Name ?? "Client inconnu",
        DeliveryNoteId = n.DeliveryNoteId,
        DeliveryNoteNumber = n.DeliveryNote?.Number.Value ?? "—",
        Status = n.Status,
        StatusDisplay = n.Status.ToDisplayString(),
        StatusCss = n.Status.ToCssClass(),
        Reason = n.Reason,
        LineCount = n.Lines.Count,
        TotalReturnedQuantity = n.TotalReturnedQuantity,
        TotalHT = n.TotalHT,
        TotalVAT = n.TotalVAT,
        TotalTTC = n.TotalTTC,
        WarehouseId = n.WarehouseId,
        WarehouseName = n.Warehouse?.Name
    };
}
