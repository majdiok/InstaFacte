using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.ProductCategories.Commands;

/// <summary>
/// Command to create a new product category.
/// </summary>
public sealed record CreateProductCategoryCommand(CreateProductCategoryDto Dto) : IRequest<Result<Guid>>;

/// <summary>
/// Validator for CreateProductCategoryCommand.
/// </summary>
public sealed class CreateProductCategoryCommandValidator : AbstractValidator<CreateProductCategoryCommand>
{
    public CreateProductCategoryCommandValidator()
    {
        RuleFor(x => x.Dto.Code)
            .NotEmpty()
            .WithMessage("Le code catégorie est obligatoire")
            .MaximumLength(50)
            .WithMessage("Le code catégorie ne peut pas dépasser 50 caractères");

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
/// Handler for CreateProductCategoryCommand.
/// </summary>
public sealed class CreateProductCategoryCommandHandler : IRequestHandler<CreateProductCategoryCommand, Result<Guid>>
{
    private readonly IProductCategoryRepository _productCategoryRepository;

    public CreateProductCategoryCommandHandler(IProductCategoryRepository productCategoryRepository)
    {
        _productCategoryRepository = productCategoryRepository;
    }

    public async Task<Result<Guid>> Handle(CreateProductCategoryCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        var code = dto.Code.Trim().ToUpperInvariant();

        var existsByCode = await _productCategoryRepository.ExistsByCodeAsync(code, cancellationToken);
        if (existsByCode)
            return Result.Failure<Guid>(Error.Conflict("Une catégorie existe déjà avec ce code."));

        var categoryResult = ProductCategory.Create(
            dto.Code.Trim(),
            dto.Name.Trim(),
            dto.DisplayOrder);

        if (categoryResult.IsFailure)
            return Result.Failure<Guid>(categoryResult.Error);

        var category = categoryResult.Value;
        await _productCategoryRepository.AddAsync(category, cancellationToken);

        return Result.Success(category.Id);
    }
}
