using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Inventory.Queries;

/// <summary>
/// Récupère le résumé pédagogique d'un inventaire avant validation.
/// </summary>
public sealed record GetInventorySummaryQuery(
    Guid InventoryId) : IRequest<Result<InventorySummaryDto>>;

/// <summary>
/// DTO pour le résumé d'inventaire.
/// </summary>
public sealed record InventorySummaryDto
{
    public Guid InventoryId { get; init; }
    public int TotalProducts { get; init; }
    public int CountedProducts { get; init; }
    public int ProductsOk { get; init; }
    public int ProductsWithDifference { get; init; }
    public int ProductsNotCounted { get; init; }
    public bool CanValidate { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public IReadOnlyList<InventorySummaryLineDto> ProductsOkList { get; init; } = Array.Empty<InventorySummaryLineDto>();
    public IReadOnlyList<InventorySummaryLineDto> ProductsWithDifferenceList { get; init; } = Array.Empty<InventorySummaryLineDto>();
    public IReadOnlyList<InventorySummaryLineDto> ProductsNotCountedList { get; init; } = Array.Empty<InventorySummaryLineDto>();
}

/// <summary>
/// Ligne du résumé avec message pédagogique.
/// </summary>
public sealed record InventorySummaryLineDto
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string? ProductCode { get; init; }
    public decimal TheoreticalQuantity { get; init; }
    public decimal? CountedQuantity { get; init; }
    public decimal Difference { get; init; }
    public string HumanMessage { get; init; } = string.Empty;
    public string DifferenceClass { get; init; } = string.Empty; // "positive", "negative", "neutral"
    public Guid? ProductLotId { get; init; }
    public string? LotNumber { get; init; }
}

public sealed class GetInventorySummaryQueryHandler : IRequestHandler<GetInventorySummaryQuery, Result<InventorySummaryDto>>
{
    private readonly IPhysicalInventoryRepository _inventoryRepository;
    private readonly ITenantContext _tenantContext;

    public GetInventorySummaryQueryHandler(
        IPhysicalInventoryRepository inventoryRepository,
        ITenantContext tenantContext)
    {
        _inventoryRepository = inventoryRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Result<InventorySummaryDto>> Handle(GetInventorySummaryQuery request, CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is null)
            return Result.Failure<InventorySummaryDto>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var inventory = await _inventoryRepository.GetWithLinesAsync(request.InventoryId, cancellationToken);
        if (inventory == null)
            return Result.Failure<InventorySummaryDto>(Error.NotFound("Inventaire", request.InventoryId));

        var summary = inventory.GetSummary();

        var productsOkList = inventory.CountLines
            .Where(l => l.IsCounted && l.Difference == 0)
            .Select(l => MapSummaryLine(l, "neutral"))
            .ToList();

        var productsWithDiffList = inventory.CountLines
            .Where(l => l.IsCounted && l.Difference != 0)
            .Select(l => MapSummaryLine(l, l.Difference > 0 ? "positive" : "negative"))
            .ToList();

        var productsNotCountedList = inventory.CountLines
            .Where(l => !l.IsCounted)
            .Select(l => MapSummaryLine(l, "neutral"))
            .ToList();

        var canValidate = inventory.Status == InventoryStatus.InProgress;
        var notCounted = summary.ProductsNotCounted.Count;
        var statusMessage = notCounted == 0
            ? "✅ Prêt à valider ! Tous les produits ont été comptés."
            : $"Prêt à valider. {notCounted} article{(notCounted > 1 ? "s" : "")} non saisi{(notCounted > 1 ? "s" : "")} seront confirmés à la quantité système.";

        return Result.Success(new InventorySummaryDto
        {
            InventoryId = inventory.Id,
            TotalProducts = summary.TotalProducts,
            CountedProducts = summary.CountedProducts,
            ProductsOk = summary.ProductsOk.Count,
            ProductsWithDifference = summary.ProductsWithDifference.Count,
            ProductsNotCounted = summary.ProductsNotCounted.Count,
            CanValidate = canValidate,
            StatusMessage = statusMessage,
            ProductsOkList = productsOkList,
            ProductsWithDifferenceList = productsWithDiffList,
            ProductsNotCountedList = productsNotCountedList
        });
    }

    private static InventorySummaryLineDto MapSummaryLine(InventoryCountLine line, string differenceClass) =>
        new()
        {
            ProductId = line.ProductId,
            ProductName = line.ProductName,
            ProductCode = line.ProductCode,
            TheoreticalQuantity = line.TheoreticalQuantity,
            CountedQuantity = line.IsCounted ? line.CountedQuantity : null,
            Difference = line.IsCounted ? line.Difference : 0,
            HumanMessage = line.GetHumanMessage(),
            DifferenceClass = differenceClass,
            ProductLotId = line.ProductLotId,
            LotNumber = line.LotNumber
        };
}
