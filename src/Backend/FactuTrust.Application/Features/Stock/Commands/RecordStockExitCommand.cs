using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Stock.Commands;

/// <summary>
/// Command to record a stock exit (manual out, damage, transfer).
/// Note: Sale exits are handled automatically via InvoiceValidatedEvent.
/// </summary>
public sealed record RecordStockExitCommand(
    Guid ProductId,
    Guid? WarehouseId,
    decimal Quantity,
    MovementReason Reason,
    string? Reference,
    string? Notes) : IRequest<Result>;

/// <summary>
/// Validator for RecordStockExitCommand.
/// </summary>
public sealed class RecordStockExitCommandValidator : AbstractValidator<RecordStockExitCommand>
{
    public RecordStockExitCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("L'identifiant du produit est obligatoire");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("La quantité doit être positive");
        RuleFor(x => x.Reason).Must(r => r is MovementReason.Damage or MovementReason.SupplierReturn or MovementReason.Transfer)
            .WithMessage("Raison invalide pour une sortie manuelle de stock (utilisez Sale uniquement via facturation)");
    }
}

/// <summary>
/// Handler for RecordStockExitCommand.
/// </summary>
public sealed class RecordStockExitCommandHandler : IRequestHandler<RecordStockExitCommand, Result>
{
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;

    public RecordStockExitCommandHandler(
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

    public async Task<Result> Handle(RecordStockExitCommand request, CancellationToken cancellationToken)
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

        // Record the exit
        var exitResult = stockItem.RecordExit(request.Quantity, request.Reason, request.Reference, request.Notes);
        if (exitResult.IsFailure)
            return exitResult;

        await _stockItemRepository.UpdateAsync(stockItem, cancellationToken);

        return Result.Success();
    }
}
