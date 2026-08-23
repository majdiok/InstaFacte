using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
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

public sealed class RecordStockEntryCommandHandler : IRequestHandler<RecordStockEntryCommand, Result<Guid>>
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IStockMutationService _mutation;

    public RecordStockEntryCommandHandler(
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        ITenantContext tenantContext,
        IStockMutationService mutation)
    {
        _warehouseRepository = warehouseRepository;
        _productRepository = productRepository;
        _tenantContext = tenantContext;
        _mutation = mutation;
    }

    public async Task<Result<Guid>> Handle(RecordStockEntryCommand request, CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is null)
            return Result.Failure<Guid>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
            return Result.Failure<Guid>(Error.NotFound("Produit", request.ProductId));

        if (!product.IsStockManaged)
            return Result.Failure<Guid>(Error.Validation("Product", "Ce produit n'a pas la gestion de stock activée."));

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

        var mutation = await _mutation.ApplyAsync(new StockMutationRequest
        {
            ProductId = request.ProductId,
            WarehouseId = warehouseId,
            Kind = StockMutationKind.Entry,
            Quantity = request.Quantity,
            UnitCost = request.UnitCost,
            Reason = request.Reason,
            Reference = request.Reference,
            Notes = request.Notes
        }, cancellationToken);

        if (mutation.IsFailure)
            return Result.Failure<Guid>(mutation.Error);

        return Result.Success(mutation.Value.StockItemId);
    }
}
