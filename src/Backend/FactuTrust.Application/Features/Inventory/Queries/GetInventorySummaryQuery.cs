using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
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

        var productsOkList = summary.ProductsOk.Select(p => new InventorySummaryLineDto
        {
            ProductId = p.ProductId,
            ProductName = p.ProductName,
            ProductCode = p.ProductCode,
            TheoreticalQuantity = inventory.CountLines.First(l => l.ProductId == p.ProductId).TheoreticalQuantity,
            CountedQuantity = inventory.CountLines.First(l => l.ProductId == p.ProductId).CountedQuantity,
            Difference = 0,
            HumanMessage = p.HumanMessage,
            DifferenceClass = "neutral"
        }).ToList();

        var productsWithDiffList = summary.ProductsWithDifference.Select(p => new InventorySummaryLineDto
        {
            ProductId = p.ProductId,
            ProductName = p.ProductName,
            ProductCode = p.ProductCode,
            TheoreticalQuantity = inventory.CountLines.First(l => l.ProductId == p.ProductId).TheoreticalQuantity,
            CountedQuantity = inventory.CountLines.First(l => l.ProductId == p.ProductId).CountedQuantity,
            Difference = p.Difference,
            HumanMessage = p.HumanMessage,
            DifferenceClass = p.Difference > 0 ? "positive" : "negative"
        }).ToList();

        var productsNotCountedList = summary.ProductsNotCounted.Select(p => new InventorySummaryLineDto
        {
            ProductId = p.ProductId,
            ProductName = p.ProductName,
            ProductCode = p.ProductCode,
            TheoreticalQuantity = inventory.CountLines.First(l => l.ProductId == p.ProductId).TheoreticalQuantity,
            CountedQuantity = null,
            Difference = 0,
            HumanMessage = p.HumanMessage,
            DifferenceClass = "neutral"
        }).ToList();

        var canValidate = inventory.IsComplete;
        var statusMessage = canValidate
            ? "✅ Prêt à valider ! Tous les produits ont été comptés."
            : $"⚠️ Il reste {summary.ProductsNotCounted.Count} produit{(summary.ProductsNotCounted.Count > 1 ? "s" : "")} à compter.";

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
}
