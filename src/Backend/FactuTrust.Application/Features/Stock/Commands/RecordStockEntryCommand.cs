using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Stock.Commands;

/// <summary>
/// Command to record a stock entry (purchase, return, initial stock).
/// </summary>
public sealed record RecordStockEntryCommand(
    Guid ProductId,
    Guid? WarehouseId,
    decimal Quantity,
    decimal UnitCost,
    MovementReason Reason,
    string? Reference,
    string? Notes) : IRequest<Result<Guid>>;

/// <summary>
/// Validator for RecordStockEntryCommand.
/// </summary>
public sealed class RecordStockEntryCommandValidator : AbstractValidator<RecordStockEntryCommand>
{
    public RecordStockEntryCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("L'identifiant du produit est obligatoire");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("La quantité doit être positive");
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).WithMessage("Le coût unitaire ne peut pas être négatif");
        RuleFor(x => x.Reason).Must(r => r is MovementReason.Purchase or MovementReason.CustomerReturn or MovementReason.InitialStock or MovementReason.Transfer)
            .WithMessage("Raison invalide pour une entrée de stock");
    }
}

/// <summary>
/// Handler for RecordStockEntryCommand.
/// </summary>
public sealed class RecordStockEntryCommandHandler : IRequestHandler<RecordStockEntryCommand, Result<Guid>>
{
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;

    public RecordStockEntryCommandHandler(
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

    public async Task<Result<Guid>> Handle(RecordStockEntryCommand request, CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is null)
            return Result.Failure<Guid>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        // Validate product exists and is stock-managed
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
            return Result.Failure<Guid>(Error.NotFound("Produit", request.ProductId));

        if (!product.IsStockManaged)
            return Result.Failure<Guid>(Error.Validation("Product", "Ce produit n'a pas la gestion de stock activée."));

        // Determine warehouse (use default if not specified)
        Guid warehouseId;
        if (request.WarehouseId.HasValue)
        {
            var warehouseExists = await _warehouseRepository.ExistsAsync(request.WarehouseId.Value, cancellationToken);
            if (!warehouseExists)
                return Result.Failure<Guid>(Error.NotFound("Entrepôt", request.WarehouseId.Value));
            warehouseId = request.WarehouseId.Value;
        }
        else
        {
            var defaultWarehouse = await _warehouseRepository.GetDefaultAsync(cancellationToken);
            if (defaultWarehouse == null)
                return Result.Failure<Guid>(Error.Validation("Warehouse", "Aucun entrepôt par défaut n'est configuré."));
            warehouseId = defaultWarehouse.Id;
        }

        // Get or create stock item
        var stockItem = await _stockItemRepository.GetByProductAndWarehouseAsync(request.ProductId, warehouseId, cancellationToken);
        
        if (stockItem == null)
        {
            // Create new stock item
            var createResult = StockItem.Create(request.ProductId, warehouseId);
            if (createResult.IsFailure)
                return Result.Failure<Guid>(createResult.Error);

            stockItem = createResult.Value;
            try
            {
                await _stockItemRepository.AddAsync(stockItem, cancellationToken);
            }
            catch (Exception ex) when (IsUniqueConstraintViolation(ex))
            {
                var existingStockItem = await _stockItemRepository.GetByProductAndWarehouseAsync(
                    request.ProductId,
                    warehouseId,
                    cancellationToken);

                if (existingStockItem != null)
                {
                    stockItem = existingStockItem;
                }
                else
                {
                    throw;
                }
            }
        }

        // Record the entry
        var entryResult = stockItem.RecordEntry(request.Quantity, request.UnitCost, request.Reason, request.Reference, request.Notes);
        if (entryResult.IsFailure)
            return Result.Failure<Guid>(entryResult.Error);

        await _stockItemRepository.UpdateAsync(stockItem, cancellationToken);

        return Result.Success(stockItem.Id);
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
