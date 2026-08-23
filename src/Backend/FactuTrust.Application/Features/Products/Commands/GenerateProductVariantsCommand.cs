using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Services;
using MediatR;

namespace FactuTrust.Application.Features.Products.Commands;

public sealed record ProductAttributeValueInput(string Code, string Name);

public sealed record CreateProductAttributeCommand(
    string Code,
    string Name,
    IReadOnlyList<ProductAttributeValueInput> Values) : IRequest<Result<Guid>>;

public sealed class CreateProductAttributeCommandHandler
    : IRequestHandler<CreateProductAttributeCommand, Result<Guid>>
{
    private readonly IProductAttributeRepository _attributes;
    private readonly ICurrentUser _currentUser;

    public CreateProductAttributeCommandHandler(
        IProductAttributeRepository attributes,
        ICurrentUser currentUser)
    {
        _attributes = attributes;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateProductAttributeCommand request, CancellationToken cancellationToken)
    {
        var created = ProductAttributeDefinition.Create(request.Code, request.Name);
        if (created.IsFailure)
            return Result.Failure<Guid>(created.Error);

        var definition = created.Value;
        definition.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");

        var order = 0;
        foreach (var value in request.Values)
        {
            var added = definition.AddValue(value.Code, value.Name, order++);
            if (added.IsFailure)
                return Result.Failure<Guid>(added.Error);
        }

        await _attributes.AddAsync(definition, cancellationToken);
        return Result.Success(definition.Id);
    }
}

public sealed record GenerateProductVariantsCommand(
    Guid ParentProductId,
    IReadOnlyList<GenerateProductVariantAxis> Axes) : IRequest<Result<IReadOnlyList<Guid>>>;

public sealed record GenerateProductVariantAxis(Guid DefinitionId, IReadOnlyList<Guid> ValueIds);

public sealed class GenerateProductVariantsCommandHandler
    : IRequestHandler<GenerateProductVariantsCommand, Result<IReadOnlyList<Guid>>>
{
    private readonly IProductRepository _products;
    private readonly IProductAttributeRepository _attributes;
    private readonly ICurrentUser _currentUser;

    public GenerateProductVariantsCommandHandler(
        IProductRepository products,
        IProductAttributeRepository attributes,
        ICurrentUser currentUser)
    {
        _products = products;
        _attributes = attributes;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<Guid>>> Handle(
        GenerateProductVariantsCommand request,
        CancellationToken cancellationToken)
    {
        var parent = await _products.GetByIdAsync(request.ParentProductId, cancellationToken);
        if (parent is null)
            return Result.Failure<IReadOnlyList<Guid>>(Error.NotFound("Product", request.ParentProductId));

        if (!parent.IsVariantTemplate)
        {
            var marked = parent.MarkAsVariantTemplate();
            if (marked.IsFailure)
                return Result.Failure<IReadOnlyList<Guid>>(marked.Error);
            await _products.UpdateAsync(parent, cancellationToken);
        }

        if (request.Axes.Count == 0)
            return Result.Failure<IReadOnlyList<Guid>>(Error.Validation("Axes", "Au moins un axe de variante est obligatoire"));

        var axisValues = new List<IReadOnlyList<ProductAttributeValue>>();
        var sort = 0;
        foreach (var axis in request.Axes)
        {
            var definition = await _attributes.GetByIdWithValuesAsync(axis.DefinitionId, cancellationToken);
            if (definition is null)
                return Result.Failure<IReadOnlyList<Guid>>(Error.NotFound("ProductAttributeDefinition", axis.DefinitionId));

            var selected = definition.Values.Where(v => axis.ValueIds.Contains(v.Id)).OrderBy(v => v.SortOrder).ToList();
            if (selected.Count == 0)
                return Result.Failure<IReadOnlyList<Guid>>(Error.Validation("ValueIds", $"Aucune valeur pour l'attribut {definition.Name}"));

            var existingAxes = await _attributes.ListAxesAsync(parent.Id, cancellationToken);
            if (existingAxes.All(a => a.DefinitionId != definition.Id))
            {
                var axisEntity = ProductVariantAxis.Create(parent.Id, definition.Id, sort++);
                if (axisEntity.IsFailure)
                    return Result.Failure<IReadOnlyList<Guid>>(axisEntity.Error);
                await _attributes.AddAxisAsync(axisEntity.Value, cancellationToken);
            }

            axisValues.Add(selected);
        }

        var combinations = Cartesian(axisValues);
        var createdIds = new List<Guid>();

        foreach (var combo in combinations)
        {
            var baseCode = ProductVariantSku.Build(parent.Code, combo.Select(v => v.Code).ToList());
            if (baseCode.IsFailure)
                return Result.Failure<IReadOnlyList<Guid>>(baseCode.Error);

            var existingExact = await _products.GetByCodeAsync(baseCode.Value, cancellationToken);
            if (existingExact is not null && existingExact.ParentProductId == parent.Id)
                continue;

            var suffix = 0;
            Result<string> codeResult;
            do
            {
                codeResult = ProductVariantSku.Build(parent.Code, combo.Select(v => v.Code).ToList(), suffix > 0 ? suffix : null);
                if (codeResult.IsFailure)
                    return Result.Failure<IReadOnlyList<Guid>>(codeResult.Error);
                suffix++;
            } while (await _products.GetByCodeAsync(codeResult.Value, cancellationToken) is not null);

            var name = $"{parent.Name} ({string.Join(" / ", combo.Select(v => v.Name))})";
            var child = Product.Create(
                codeResult.Value,
                name,
                parent.Type,
                parent.UnitPrice,
                parent.VatRate,
                parent.CategoryId,
                parent.Description,
                parent.Unit,
                isStockManaged: true,
                parent.PurchasePrice,
                parent.IsFodecApplicable,
                parent.ProfitMarginPercent,
                parent.IsDiscountEnabled,
                parent.MaxDiscountPercent);

            if (child.IsFailure)
                return Result.Failure<IReadOnlyList<Guid>>(child.Error);

            var attach = child.Value.AttachToParent(parent.Id);
            if (attach.IsFailure)
                return Result.Failure<IReadOnlyList<Guid>>(attach.Error);

            var trace = child.Value.ConfigureTraceability(
                parent.TrackingMode,
                parent.HasExpiryTracking,
                parent.PickingPolicy,
                parent.CostingMethod,
                parent.ExpiryAlertDays);
            if (trace.IsFailure)
                return Result.Failure<IReadOnlyList<Guid>>(trace.Error);

            child.Value.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");
            await _products.AddAsync(child.Value, cancellationToken);

            foreach (var value in combo)
            {
                var link = ProductVariantAttributeValue.Create(child.Value.Id, value.Id);
                if (link.IsFailure)
                    return Result.Failure<IReadOnlyList<Guid>>(link.Error);
                await _attributes.AddVariantLinkAsync(link.Value, cancellationToken);
            }

            createdIds.Add(child.Value.Id);
        }

        return Result.Success<IReadOnlyList<Guid>>(createdIds);
    }

    private static IReadOnlyList<IReadOnlyList<ProductAttributeValue>> Cartesian(
        IReadOnlyList<IReadOnlyList<ProductAttributeValue>> axes)
    {
        IEnumerable<IReadOnlyList<ProductAttributeValue>> acc = new[] { Array.Empty<ProductAttributeValue>() };
        foreach (var axis in axes)
        {
            acc = acc.SelectMany(prefix => axis.Select(value =>
                (IReadOnlyList<ProductAttributeValue>)prefix.Concat(new[] { value }).ToList()));
        }

        return acc.ToList();
    }
}
