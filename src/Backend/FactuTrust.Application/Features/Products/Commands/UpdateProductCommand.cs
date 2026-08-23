using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Common.Validation;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Products;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Products.Commands;

/// <summary>
/// Command to update an existing product.
/// </summary>
public sealed record UpdateProductCommand(Guid Id, UpdateProductDto Dto) : IRequest<Result<ProductDetailDto>>;

/// <summary>
/// Validator for UpdateProductCommand.
/// </summary>
public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty).WithMessage("Identifiant produit invalide.");
        RuleFor(x => x.Dto.Name).NotEmpty().WithMessage("Le nom du produit est obligatoire");
        RuleFor(x => x.Dto.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Le prix unitaire ne peut pas être négatif");
        RuleFor(x => x.Dto.PurchasePrice)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Dto.PurchasePrice.HasValue)
            .WithMessage("Le prix d'achat ne peut pas être négatif");
        RuleFor(x => x.Dto.MaxDiscountPercent)
            .InclusiveBetween(0, TunisianValidationRules.NumericLimits.MaxDiscountPercent)
            .When(x => x.Dto.IsDiscountEnabled && x.Dto.MaxDiscountPercent.HasValue)
            .WithMessage("La remise maximale doit être comprise entre 0 % et 100 %");
        RuleFor(x => x.Dto.MaxDiscountPercent)
            .NotNull()
            .When(x => x.Dto.IsDiscountEnabled)
            .WithMessage("La remise maximale est obligatoire lorsque la remise produit est activée");
    }
}

/// <summary>
/// Handler for UpdateProductCommand.
/// </summary>
public sealed class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Result<ProductDetailDto>>
{
    private readonly IProductRepository _productRepository;
    private readonly IProductCategoryRepository _productCategoryRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public UpdateProductCommandHandler(
        IProductRepository productRepository,
        IProductCategoryRepository productCategoryRepository,
        ISupplierRepository supplierRepository,
        IStockItemRepository stockItemRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _productRepository = productRepository;
        _productCategoryRepository = productCategoryRepository;
        _supplierRepository = supplierRepository;
        _stockItemRepository = stockItemRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result<ProductDetailDto>> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product is null)
            return Result.Failure<ProductDetailDto>(Error.NotFound("Product", request.Id));

        var dto = request.Dto;

        if (dto.CategoryId.HasValue && dto.CategoryId.Value != Guid.Empty)
        {
            var categoryExists = await _productCategoryRepository.ExistsAsync(dto.CategoryId.Value, cancellationToken);
            if (!categoryExists)
                return Result.Failure<ProductDetailDto>(Error.Validation("CategoryId", "La catégorie sélectionnée n'existe pas"));
        }

        // Create Money value object
        var unitPrice = Money.Create(dto.UnitPrice, Money.DefaultCurrency);
        Money? purchasePrice = dto.PurchasePrice.HasValue
            ? Money.Create(dto.PurchasePrice.Value, Money.DefaultCurrency)
            : null;

        // Create VatRate from percent
        var vatRate = VatRateExtensions.FromPercent((int)dto.VatRate);

        // Update product
        product.Update(
            dto.Name.Trim(),
            dto.Description?.Trim(),
            unitPrice,
            vatRate,
            dto.Unit?.Trim(),
            purchasePrice,
            dto.CategoryId,
            dto.IsFodecApplicable,
            dto.IsDiscountEnabled,
            dto.MaxDiscountPercent);

        // Code-barres : clé de contrôle vérifiée à l'enregistrement. Un code invalide est
        // refusé sans laisser le produit dans un état partiellement modifié.
        var barcodeResult = product.SetBarcode(dto.Barcode);
        if (barcodeResult.IsFailure)
            return Result.Failure<ProductDetailDto>(barcodeResult.Error);

        if (dto.IsStockManaged.HasValue)
        {
            if (dto.IsStockManaged.Value)
            {
                var enableResult = product.EnableStockManagement();
                if (enableResult.IsFailure)
                    return Result.Failure<ProductDetailDto>(enableResult.Error);
            }
            else
            {
                product.DisableStockManagement();
            }
        }

        // Fournisseur préféré (optionnel) — Guid.Empty traité comme « aucun » ; null efface la préférence.
        var preferredSupplierId = dto.PreferredSupplierId is { } sid && sid != Guid.Empty ? sid : (Guid?)null;
        if (preferredSupplierId.HasValue)
        {
            var supplierExists = await _supplierRepository.ExistsAsync(preferredSupplierId.Value, cancellationToken);
            if (!supplierExists)
                return Result.Failure<ProductDetailDto>(Error.Validation("PreferredSupplierId", "Le fournisseur sélectionné n'existe pas"));
        }
        product.SetPreferredSupplier(preferredSupplierId);

        if (dto.IsVariantTemplate == true && !product.IsVariantTemplate)
        {
            var template = product.MarkAsVariantTemplate();
            if (template.IsFailure)
                return Result.Failure<ProductDetailDto>(template.Error);
        }

        if (dto.TrackingMode.HasValue || dto.HasExpiryTracking.HasValue || dto.PickingPolicy.HasValue
            || dto.CostingMethod.HasValue || dto.ExpiryAlertDays.HasValue)
        {
            var nextMode = dto.TrackingMode ?? product.TrackingMode;
            if (product.TrackingMode == TrackingMode.None && nextMode != TrackingMode.None)
            {
                var stocks = await _stockItemRepository.GetByProductAsync(product.Id, cancellationToken);
                if (stocks.Any(s => s.QuantityOnHand > 0))
                {
                    return Result.Failure<ProductDetailDto>(Error.Validation("TrackingMode",
                        "Impossible d'activer le suivi par lot/série tant qu'un stock physique existe. Passez par un inventaire d'ouverture."));
                }
            }

            var nextCosting = dto.CostingMethod ?? product.CostingMethod;
            if (product.CostingMethod == CostingMethod.Average && nextCosting is CostingMethod.Fifo or CostingMethod.Lifo)
            {
                var stocks = await _stockItemRepository.GetByProductAsync(product.Id, cancellationToken);
                if (stocks.Any(s => s.QuantityOnHand > 0))
                {
                    return Result.Failure<ProductDetailDto>(Error.Validation("CostingMethod",
                        "Le passage CMUP → FIFO/LIFO exige une couche d'ouverture (inventaire). Stock physique non nul."));
                }
            }

            var trace = product.ConfigureTraceability(
                nextMode,
                dto.HasExpiryTracking ?? product.HasExpiryTracking,
                dto.PickingPolicy ?? product.PickingPolicy,
                nextCosting,
                dto.ExpiryAlertDays ?? product.ExpiryAlertDays);
            if (trace.IsFailure)
                return Result.Failure<ProductDetailDto>(trace.Error);
        }

        product.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);

        await _productRepository.UpdateAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Product.Updated,
            "Product",
            product.Id,
            newValues: new { product.Code, product.Name, product.UnitPrice.Amount },
            cancellationToken: cancellationToken);

        var detail = ProductDetailMapper.ToDetailDto(product);
        return Result.Success(detail);
    }
}
