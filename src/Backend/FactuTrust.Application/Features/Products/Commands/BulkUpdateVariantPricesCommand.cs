using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.ValueObjects;
using MediatR;

namespace FactuTrust.Application.Features.Products.Commands;

public sealed record BulkUpdateVariantPricesCommand(
    Guid ParentProductId,
    string Mode,
    IReadOnlyList<BulkUpdateVariantPriceItem> Items) : IRequest<Result<int>>;

public sealed class BulkUpdateVariantPricesCommandHandler
    : IRequestHandler<BulkUpdateVariantPricesCommand, Result<int>>
{
    private readonly IProductRepository _products;
    private readonly ICurrentUser _currentUser;

    public BulkUpdateVariantPricesCommandHandler(
        IProductRepository products,
        ICurrentUser currentUser)
    {
        _products = products;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(
        BulkUpdateVariantPricesCommand request,
        CancellationToken cancellationToken)
    {
        var parent = await _products.GetByIdAsync(request.ParentProductId, cancellationToken);
        if (parent is null)
            return Result.Failure<int>(Error.NotFound("Product", request.ParentProductId));

        if (!parent.IsVariantTemplate)
            return Result.Failure<int>(Error.Validation("IsVariantTemplate", "Le produit parent doit être un modèle de variantes."));

        var mode = request.Mode?.Trim().ToLowerInvariant() ?? "absolute";
        var updated = 0;

        if (mode == "copyfromparent")
        {
            var (children, _) = await _products.GetChildrenByParentIdAsync(
                parent.Id, 1, 500, cancellationToken);

            foreach (var child in children)
            {
                child.UpdatePrice(parent.UnitPrice);
                child.UpdatePurchasePrice(parent.PurchasePrice);
                child.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");
                await _products.UpdateAsync(child, cancellationToken);
                updated++;
            }

            return Result.Success(updated);
        }

        if (request.Items is null || request.Items.Count == 0)
            return Result.Failure<int>(Error.Validation("Items", "Aucune variante à mettre à jour."));

        foreach (var item in request.Items)
        {
            var child = await _products.GetByIdAsync(item.ChildId, cancellationToken);
            if (child is null)
                return Result.Failure<int>(Error.NotFound("Product", item.ChildId));

            if (child.ParentProductId != parent.Id)
                return Result.Failure<int>(Error.Validation("ChildId", $"La variante {child.Code} n'appartient pas à ce modèle."));

            if (item.UnitPrice.HasValue)
                child.UpdatePrice(Money.Create(item.UnitPrice.Value, child.UnitPrice.Currency));

            if (item.PurchasePrice.HasValue)
                child.UpdatePurchasePrice(Money.Create(item.PurchasePrice.Value, child.UnitPrice.Currency));

            if (item.Barcode is not null)
            {
                var barcodeResult = child.SetBarcode(item.Barcode);
                if (barcodeResult.IsFailure)
                    return Result.Failure<int>(barcodeResult.Error);
            }

            child.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");
            await _products.UpdateAsync(child, cancellationToken);
            updated++;
        }

        return Result.Success(updated);
    }
}
