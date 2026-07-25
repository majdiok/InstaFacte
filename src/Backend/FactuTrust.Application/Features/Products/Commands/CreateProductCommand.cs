using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Common.Validation;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Products.Commands;

/// <summary>
/// Command to create a new product.
/// </summary>
public sealed record CreateProductCommand(CreateProductDto Dto) : IRequest<Result<Guid>>;

/// <summary>
/// Validator for CreateProductCommand.
/// </summary>
public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Dto.Code).NotEmpty().WithMessage("Le code produit est obligatoire");
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
/// Handler for CreateProductCommand.
/// </summary>
public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<Guid>>
{
    private readonly IProductRepository _productRepository;
    private readonly IProductCategoryRepository _productCategoryRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly ITenantContext _tenantContext;

    public CreateProductCommandHandler(
        IProductRepository productRepository,
        IProductCategoryRepository productCategoryRepository,
        ISupplierRepository supplierRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditService auditService,
        ITenantContext tenantContext)
    {
        _productRepository = productRepository;
        _productCategoryRepository = productCategoryRepository;
        _supplierRepository = supplierRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditService = auditService;
        _tenantContext = tenantContext;
    }

    public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is null)
            return Result.Failure<Guid>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var dto = request.Dto;

        // Check if code already exists
        var existingByCode = await _productRepository.GetByCodeAsync(dto.Code, cancellationToken);
        if (existingByCode is not null)
            return Result.Failure<Guid>(Error.Conflict("Un produit existe déjà avec ce code."));

        // Resolve category ID
        var categoryId = dto.CategoryId ?? await _productCategoryRepository.GetDefaultCategoryIdAsync(cancellationToken);
        if (categoryId == Guid.Empty)
            return Result.Failure<Guid>(Error.Validation("CategoryId", "La catégorie produit est obligatoire"));

        var categoryExists = await _productCategoryRepository.ExistsAsync(categoryId, cancellationToken);
        if (!categoryExists)
            return Result.Failure<Guid>(Error.Validation("CategoryId", "La catégorie sélectionnée n'existe pas"));

        // Create Money value object
        var unitPrice = Money.Create(dto.UnitPrice, Money.DefaultCurrency);
        Money? purchasePrice = dto.PurchasePrice.HasValue
            ? Money.Create(dto.PurchasePrice.Value, Money.DefaultCurrency)
            : null;

        // Create VatRate from percent
        var vatRate = VatRateExtensions.FromPercent((int)dto.VatRate);

        // Create product
        var productResult = Product.Create(
            dto.Code.Trim(),
            dto.Name.Trim(),
            dto.Type,
            unitPrice,
            vatRate,
            categoryId,
            dto.Description?.Trim(),
            dto.Unit?.Trim(),
            dto.IsStockManaged ?? false,
            purchasePrice,
            dto.IsFodecApplicable,
            dto.ProfitMarginPercent,
            dto.IsDiscountEnabled,
            dto.MaxDiscountPercent);

        if (productResult.IsFailure)
            return Result.Failure<Guid>(productResult.Error);

        var product = productResult.Value;

        // Fournisseur préféré (optionnel) — Guid.Empty traité comme « aucun ».
        var preferredSupplierId = dto.PreferredSupplierId is { } sid && sid != Guid.Empty ? sid : (Guid?)null;
        if (preferredSupplierId.HasValue)
        {
            var supplierExists = await _supplierRepository.ExistsAsync(preferredSupplierId.Value, cancellationToken);
            if (!supplierExists)
                return Result.Failure<Guid>(Error.Validation("PreferredSupplierId", "Le fournisseur sélectionné n'existe pas"));
            product.SetPreferredSupplier(preferredSupplierId);
        }

        product.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");

        await _productRepository.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Product.Created,
            "Product",
            product.Id,
            newValues: new { product.Code, product.Name, product.Type },
            cancellationToken: cancellationToken);

        return Result.Success(product.Id);
    }
}
