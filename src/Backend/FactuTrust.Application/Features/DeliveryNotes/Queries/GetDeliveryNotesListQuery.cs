using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.DeliveryNotes.Queries;

/// <summary>
/// Query to get a paginated list of delivery notes.
/// </summary>
public sealed record GetDeliveryNotesListQuery(
    int Page = 1,
    int PageSize = 10,
    Guid? ClientId = null,
    DeliveryNoteStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Search = null) : IRequest<Result<PagedResult<DeliveryNoteListDto>>>;

/// <summary>
/// Handler for GetDeliveryNotesListQuery.
/// </summary>
public sealed class GetDeliveryNotesListQueryHandler : IRequestHandler<GetDeliveryNotesListQuery, Result<PagedResult<DeliveryNoteListDto>>>
{
    private readonly IDeliveryNoteRepository _deliveryNoteRepository;

    public GetDeliveryNotesListQueryHandler(IDeliveryNoteRepository deliveryNoteRepository)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
    }

    public async Task<Result<PagedResult<DeliveryNoteListDto>>> Handle(GetDeliveryNotesListQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _deliveryNoteRepository.GetPagedAsync(
            request.Page,
            request.PageSize,
            request.ClientId,
            request.Status,
            request.FromDate,
            request.ToDate,
            request.Search,
            cancellationToken);

        var dtos = items.Select(dn => new DeliveryNoteListDto(
            dn.Id,
            dn.Number.Value,
            dn.IssueDate,
            dn.DeliveryDate,
            dn.ClientId,
            dn.Client?.Name ?? "Client inconnu",
            dn.Status,
            dn.Status.ToDisplayString(),
            dn.DeliveryAddress,
            dn.Lines.Count,
            dn.TotalOrderedQuantity,
            dn.TotalDeliveredQuantity,
            dn.TotalHT,
            dn.TotalVAT,
            dn.TotalTTC,
            !string.IsNullOrEmpty(dn.RecipientName),
            dn.InvoiceId,
            dn.Invoice?.Number?.Value,
            dn.WarehouseId,
            dn.Warehouse?.Name)).ToList();

        return Result.Success(PagedResult<DeliveryNoteListDto>.Create(dtos, request.Page, request.PageSize, totalCount));
    }
}
