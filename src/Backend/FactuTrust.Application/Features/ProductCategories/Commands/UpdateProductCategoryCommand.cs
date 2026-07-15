using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Constants;
using FactuTrust.Domain.Entities;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.ProductCategories.Commands;

/// <summary>
/// Command to update an existing product category.
/// </summary>
public sealed record UpdateProductCategoryCommand(Guid Id, UpdateProductCategoryDto Dto) : IRequest<Result<ProductCategoryDto>>;

/// <summary>
/// Validator for UpdateProductCategoryCommand.
/// </summary>
public sealed class UpdateProductCategoryCommandValidator : AbstractValidator<UpdateProductCategoryCommand>
{
    public UpdateProductCategoryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEqual(Guid.Empty)
            .WithMessage("L'identifiant de la catégorie est invalide.");

        RuleFor(x => x.Dto.Name)
            .NotEmpty()
            .WithMessage("Le nom de la catégorie est obligatoire")
            .MaximumLength(100)
            .WithMessage("Le nom de la catégorie ne peut pas dépasser 100 caractères");

        RuleFor(x => x.Dto.DisplayOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessage("L'ordre d'affichage ne peut pas être négatif");
    }
}

/// <summary>
/// Handler for UpdateProductCategoryCommand.
/// </summary>
public sealed class UpdateProductCategoryCommandHandler : IRequestHandler<UpdateProductCategoryCommand, Result<ProductCategoryDto>>
{
    private readonly IProductCategoryRepository _productCategoryRepository;

    public UpdateProductCategoryCommandHandler(IProductCategoryRepository productCategoryRepository)
    {
        _productCategoryRepository = productCategoryRepository;
    }

    public async Task<Result<ProductCategoryDto>> Handle(UpdateProductCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _productCategoryRepository.GetByIdAsync(request.Id, cancellationToken);
        if (category is null)
            return Result.Failure<ProductCategoryDto>(Error.NotFound("ProductCategory", request.Id));

        var dto = request.Dto;

        if (!dto.IsActive && category.Id == ProductCategoryConstants.DefaultCategoryId)
            return Result.Failure<ProductCategoryDto>(Error.Validation("IsActive", "La catégorie par défaut ne peut pas être désactivée."));

        category.Update(dto.Name, dto.DisplayOrder);
        if (dto.IsActive)
            category.Reactivate();
        else
            category.Deactivate();

        await _productCategoryRepository.UpdateAsync(category, cancellationToken);

        var resultDto = new ProductCategoryDto
        {
            Id = category.Id,
            Code = category.Code,
            Name = category.Name,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive
        };

        return Result.Success(resultDto);
    }
}
