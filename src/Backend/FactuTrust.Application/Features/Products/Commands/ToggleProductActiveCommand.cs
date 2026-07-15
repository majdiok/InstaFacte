using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Products.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Products.Commands;

/// <summary>
/// Command to toggle product active status.
/// </summary>
public sealed record ToggleProductActiveCommand(Guid Id) : IRequest<Result<ProductDetailDto>>;

/// <summary>
/// Validator for ToggleProductActiveCommand.
/// </summary>
public sealed class ToggleProductActiveCommandValidator : AbstractValidator<ToggleProductActiveCommand>
{
    public ToggleProductActiveCommandValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty).WithMessage("Identifiant produit invalide.");
    }
}

/// <summary>
/// Handler for ToggleProductActiveCommand.
/// </summary>
public sealed class ToggleProductActiveCommandHandler : IRequestHandler<ToggleProductActiveCommand, Result<ProductDetailDto>>
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public ToggleProductActiveCommandHandler(
        IProductRepository productRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result<ProductDetailDto>> Handle(ToggleProductActiveCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product is null)
            return Result.Failure<ProductDetailDto>(Error.NotFound("Product", request.Id));

        if (product.IsActive)
            product.Deactivate();
        else
            product.Reactivate();

        product.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);

        await _productRepository.UpdateAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            product.IsActive ? AuditActions.Product.Updated : AuditActions.Product.Updated,
            "Product",
            product.Id,
            newValues: new { product.IsActive },
            cancellationToken: cancellationToken);

        var detail = new ProductDetailDto
        {
            Id = product.Id,
            Code = product.Code,
            Name = product.Name,
            Description = product.Description,
            Type = product.Type,
            TypeDisplay = product.Type.ToDisplayString(),
            UnitPrice = product.UnitPrice.Amount,
            Currency = product.UnitPrice.Currency,
            VatRate = product.VatRate,
            VatRatePercent = (int)product.VatRate,
            VatRateDisplay = product.VatRate.ToDisplayString(),
            Unit = product.Unit,
            IsActive = product.IsActive,
            IsStockManaged = product.IsStockManaged,
            IsFodecApplicable = product.IsFodecApplicable,
            PreferredSupplierId = product.PreferredSupplierId,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };

        return Result.Success(detail);
    }
}
