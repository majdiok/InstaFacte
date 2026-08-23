using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
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

public sealed class RecordStockExitCommandHandler : IRequestHandler<RecordStockExitCommand, Result>
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IStockMutationService _mutation;

    public RecordStockExitCommandHandler(
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

    public async Task<Result> Handle(RecordStockExitCommand request, CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is null)
            return Result.Failure(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
            return Result.Failure(Error.NotFound("Produit", request.ProductId));

        if (!product.IsStockManaged)
            return Result.Failure(Error.Validation("Product", "Ce produit n'a pas la gestion de stock activée."));

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

        var mutation = await _mutation.ApplyAsync(new StockMutationRequest
        {
            ProductId = request.ProductId,
            WarehouseId = warehouseId,
            Kind = StockMutationKind.Exit,
            Quantity = request.Quantity,
            Reason = request.Reason,
            Reference = request.Reference,
            Notes = request.Notes
        }, cancellationToken);

        return mutation.IsFailure ? Result.Failure(mutation.Error) : Result.Success();
    }
}
