using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Stock.Commands;

/// <summary>
/// Command to adjust stock (inventory correction).
/// </summary>
public sealed record AdjustStockCommand(
    Guid ProductId,
    Guid? WarehouseId,
    decimal NewQuantity,
    string? Notes) : IRequest<Result>;

/// <summary>
/// Validator for AdjustStockCommand.
/// </summary>
public sealed class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("L'identifiant du produit est obligatoire");
        RuleFor(x => x.NewQuantity).GreaterThanOrEqualTo(0).WithMessage("La nouvelle quantité ne peut pas être négative");
    }
}

/// <summary>
/// Handler for AdjustStockCommand.
/// </summary>
public sealed class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, Result>
{
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;

    public AdjustStockCommandHandler(
        IStockItemRepository stockItemRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        ITenantContext tenantContext)
    {
        _stockItemRepository = stockItemRepository;
        _warehouseRepository = warehouseRepository;
        _productRepository = productRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Result> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is null)
            return Result.Failure(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        // Validate product exists
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
            return Result.Failure(Error.NotFound("Produit", request.ProductId));

        if (!product.IsStockManaged)
            return Result.Failure(Error.Validation("Product", "Ce produit n'a pas la gestion de stock activée."));

        // Determine warehouse
        Guid warehouseId;
        if (request.WarehouseId.HasValue)
        {
            warehouseId = request.WarehouseId.Value;
        }
        else
        {
            var defaultWarehouse = await _warehouseRepository.GetDefaultAsync(cancellationToken);
            if (defaultWarehouse == null)
                return Result.Failure(Error.Validation("Warehouse", "Aucun entrepôt par défaut n'est configuré."));
            warehouseId = defaultWarehouse.Id;
        }

        // Get stock item
        var stockItem = await _stockItemRepository.GetByProductAndWarehouseAsync(request.ProductId, warehouseId, cancellationToken);
        if (stockItem == null)
            return Result.Failure(Error.Validation("StockItem", "Aucun stock trouvé pour ce produit dans cet entrepôt."));

        // Adjust stock
        var adjustResult = stockItem.AdjustStock(request.NewQuantity, request.Notes);
        if (adjustResult.IsFailure)
            return adjustResult;

        await _stockItemRepository.UpdateAsync(stockItem, cancellationToken);

        return Result.Success();
    }
}
