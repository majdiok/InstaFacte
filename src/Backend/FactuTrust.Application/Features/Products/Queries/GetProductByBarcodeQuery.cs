using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.ValueObjects;
using MediatR;

namespace FactuTrust.Application.Features.Products.Queries;

/// <summary>
/// Recherche un article par code-barres, en correspondance EXACTE.
///
/// Aucune approximation : un scan qui ne correspond à rien doit échouer bruyamment. Le scan
/// du point de vente interrogeait auparavant le code produit interne avec repli sur un
/// <c>includes()</c> bidirectionnel, et pouvait donc encaisser un autre article.
/// </summary>
public sealed record GetProductByBarcodeQuery(string Barcode) : IRequest<Result<ProductDetailDto>>;

public sealed class GetProductByBarcodeQueryHandler
    : IRequestHandler<GetProductByBarcodeQuery, Result<ProductDetailDto>>
{
    private readonly IProductRepository _productRepository;

    public GetProductByBarcodeQueryHandler(IProductRepository productRepository)
        => _productRepository = productRepository;

    public async Task<Result<ProductDetailDto>> Handle(
        GetProductByBarcodeQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Barcode))
            return Result.Failure<ProductDetailDto>(Error.Validation("Barcode", "Code-barres manquant"));

        var normalized = request.Barcode.Trim();

        // Format vérifié avant d'interroger la base : un code mal formé n'a aucune chance de
        // correspondre, et le refuser tout de suite donne un message utile au caissier.
        if (!Barcode.IsValid(normalized))
        {
            return Result.Failure<ProductDetailDto>(Error.Validation("Barcode",
                $"Code-barres invalide : {normalized}"));
        }

        var product = await _productRepository.GetByBarcodeAsync(normalized, cancellationToken);

        if (product is null)
            return Result.Failure<ProductDetailDto>(Error.NotFound("Product", Guid.Empty));

        return Result.Success(ProductDetailMapper.ToDetailDto(product));
    }
}
