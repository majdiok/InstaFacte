using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Stock.Queries;

/// <summary>
/// Query to get movement history for a stock item.
/// </summary>
public sealed record GetStockMovementsQuery(
    Guid StockItemId,
    int Page = 1,
    int PageSize = 20) : IRequest<StockMovementsResult>;

/// <summary>
/// Result for stock movements query.
/// </summary>
public sealed record StockMovementsResult(
    IReadOnlyList<StockMovementDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>
/// DTO for stock movement.
/// </summary>
public sealed record StockMovementDto(
    Guid Id,
    string Type,
    string Reason,
    decimal Quantity,
    decimal UnitCost,
    decimal TotalCost,
    decimal BalanceAfter,
    string? Reference,
    string? Notes,
    DateTime OccurredAt,
    /// <summary>Quantité vendue non honorée faute de stock. Null en fonctionnement normal.</summary>
    decimal? ShortfallQuantity = null);

/// <summary>
/// Handler for GetStockMovementsQuery.
/// </summary>
public sealed class GetStockMovementsQueryHandler : IRequestHandler<GetStockMovementsQuery, StockMovementsResult>
{
    private readonly IStockMovementRepository _movementRepository;

    public GetStockMovementsQueryHandler(IStockMovementRepository movementRepository)
    {
        _movementRepository = movementRepository;
    }

    public async Task<StockMovementsResult> Handle(GetStockMovementsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _movementRepository.GetByStockItemAsync(
            request.StockItemId,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(m => new StockMovementDto(
            m.Id,
            m.Type == MovementType.Entry ? "Entry" : m.Type == MovementType.Exit ? "Exit" : "Adjustment",
            GetReasonLabel(m.Reason),
            m.Quantity,
            m.UnitCost,
            m.Quantity * m.UnitCost,
            m.BalanceAfter,
            m.Reference,
            m.Notes,
            m.OccurredAt,
            m.ShortfallQuantity
        )).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

        return new StockMovementsResult(dtos, totalCount, request.Page, request.PageSize, totalPages);
    }

    private static string GetReasonLabel(MovementReason reason)
    {
        return reason switch
        {
            MovementReason.Purchase => "Achat",
            MovementReason.Sale => "Vente",
            MovementReason.CustomerReturn => "Retour Client",
            MovementReason.SupplierReturn => "Retour Fournisseur",
            MovementReason.InventoryAdjustment => "Ajustement",
            MovementReason.Transfer => "Transfert",
            MovementReason.Damage => "Dommage/Perte",
            MovementReason.InitialStock => "Stock Initial",
            MovementReason.Delivery => "Livraison",
            MovementReason.InternalUse => "Consommation interne",
            MovementReason.GiftOrSample => "Don / échantillon",
            MovementReason.FoundOrOther => "Trouvé / autre",
            _ => reason.ToString()
        };
    }
}
