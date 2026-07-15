using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Inventory.Commands;

/// <summary>
/// Valide l'inventaire et applique les ajustements de stock.
/// Tous les produits doivent avoir été comptés.
/// </summary>
public sealed record ValidateInventoryCommand(
    Guid InventoryId) : IRequest<Result<ValidateInventoryResult>>;

/// <summary>
/// Résultat de la validation avec résumé pédagogique.
/// </summary>
public sealed record ValidateInventoryResult
{
    public Guid InventoryId { get; init; }
    public int TotalProducts { get; init; }
    public int ProductsWithChanges { get; init; }
    public int ProductsOk { get; init; }
    public string HumanMessage { get; init; } = string.Empty;
    public IReadOnlyList<AdjustmentSummaryItem> Adjustments { get; init; } = Array.Empty<AdjustmentSummaryItem>();
}

/// <summary>
/// Résumé d'un ajustement de stock.
/// </summary>
public sealed record AdjustmentSummaryItem
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal PreviousQuantity { get; init; }
    public decimal NewQuantity { get; init; }
    public decimal Difference { get; init; }
    public string HumanMessage { get; init; } = string.Empty;
}

public sealed class ValidateInventoryCommandValidator : AbstractValidator<ValidateInventoryCommand>
{
    public ValidateInventoryCommandValidator()
    {
        RuleFor(x => x.InventoryId)
            .NotEmpty()
            .WithMessage("L'identifiant de l'inventaire est obligatoire.");
    }
}

public sealed class ValidateInventoryCommandHandler : IRequestHandler<ValidateInventoryCommand, Result<ValidateInventoryResult>>
{
    private readonly IPhysicalInventoryRepository _inventoryRepository;
    private readonly IStockItemRepository _stockItemRepository;
    private readonly ITenantContext _tenantContext;

    public ValidateInventoryCommandHandler(
        IPhysicalInventoryRepository inventoryRepository,
        IStockItemRepository stockItemRepository,
        ITenantContext tenantContext)
    {
        _inventoryRepository = inventoryRepository;
        _stockItemRepository = stockItemRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Result<ValidateInventoryResult>> Handle(ValidateInventoryCommand request, CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is null)
            return Result.Failure<ValidateInventoryResult>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        // Get inventory with lines
        var inventory = await _inventoryRepository.GetWithLinesAsync(request.InventoryId, cancellationToken);
        if (inventory == null)
            return Result.Failure<ValidateInventoryResult>(Error.NotFound("Inventaire", request.InventoryId));

        // Validate the inventory (checks all products are counted)
        var validateResult = inventory.Validate();
        if (validateResult.IsFailure)
            return Result.Failure<ValidateInventoryResult>(validateResult.Error);

        // Apply stock adjustments for each product with difference
        var adjustments = new List<AdjustmentSummaryItem>();
        var linesWithDifference = inventory.CountLines.Where(l => l.Difference != 0).ToList();

        foreach (var line in linesWithDifference)
        {
            if (!line.CountedQuantity.HasValue)
                continue;

            var stockItem = await _stockItemRepository.GetByProductAndWarehouseAsync(
                line.ProductId, inventory.WarehouseId, cancellationToken);

            if (stockItem == null)
            {
                var createResult = StockItem.Create(line.ProductId, inventory.WarehouseId);
                if (createResult.IsFailure)
                    return Result.Failure<ValidateInventoryResult>(createResult.Error);

                stockItem = createResult.Value;
                try
                {
                    await _stockItemRepository.AddAsync(stockItem, cancellationToken);
                }
                catch (Exception ex) when (IsUniqueConstraintViolation(ex))
                {
                    var existing = await _stockItemRepository.GetByProductAndWarehouseAsync(
                        line.ProductId, inventory.WarehouseId, cancellationToken);
                    if (existing != null)
                        stockItem = existing;
                    else
                        throw;
                }
            }

            var adjustResult = stockItem.AdjustStock(line.CountedQuantity.Value,
                $"Ajustement inventaire #{inventory.Id:N}");

            if (adjustResult.IsFailure)
                return Result.Failure<ValidateInventoryResult>(adjustResult.Error);

            await _stockItemRepository.UpdateAsync(stockItem, cancellationToken);

            adjustments.Add(new AdjustmentSummaryItem
            {
                ProductId = line.ProductId,
                ProductName = line.ProductName,
                PreviousQuantity = line.TheoreticalQuantity,
                NewQuantity = line.CountedQuantity.Value,
                Difference = line.Difference,
                HumanMessage = line.GetHumanMessage()
            });
        }

        await _inventoryRepository.UpdateAsync(inventory, cancellationToken);

        // Generate summary message
        var productsOk = inventory.CountLines.Count(l => l.Difference == 0);
        var humanMessage = GenerateSummaryMessage(inventory.TotalProducts, linesWithDifference.Count, productsOk);

        return Result.Success(new ValidateInventoryResult
        {
            InventoryId = inventory.Id,
            TotalProducts = inventory.TotalProducts,
            ProductsWithChanges = linesWithDifference.Count,
            ProductsOk = productsOk,
            HumanMessage = humanMessage,
            Adjustments = adjustments
        });
    }

    private static string GenerateSummaryMessage(int total, int withChanges, int ok)
    {
        if (withChanges == 0)
            return $"🎉 Parfait ! Tous vos {total} produits correspondent exactement au système. Aucun ajustement nécessaire.";

        if (ok == 0)
            return $"✅ Inventaire terminé ! Le stock de {withChanges} produit{(withChanges > 1 ? "s" : "")} a été ajusté.";

        return $"✅ Inventaire terminé ! {ok} produit{(ok > 1 ? "s" : "")} OK, {withChanges} ajusté{(withChanges > 1 ? "s" : "")}.";
    }

    private static bool IsUniqueConstraintViolation(Exception exception)
    {
        var current = exception;
        while (current != null)
        {
            if (IsUniqueConstraintMessage(current.Message))
                return true;

            current = current.InnerException;
        }

        return false;
    }

    private static bool IsUniqueConstraintMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return message.Contains("IX_StockItems_ProductId_WarehouseId", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }
}
