using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.Stock.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.Products.Commands;

public sealed record GetProductAttributeByIdQuery(Guid Id) : IRequest<Result<ProductAttributeDto>>;

public sealed class GetProductAttributeByIdQueryHandler
    : IRequestHandler<GetProductAttributeByIdQuery, Result<ProductAttributeDto>>
{
    private readonly IProductAttributeRepository _attributes;

    public GetProductAttributeByIdQueryHandler(IProductAttributeRepository attributes)
    {
        _attributes = attributes;
    }

    public async Task<Result<ProductAttributeDto>> Handle(
        GetProductAttributeByIdQuery request,
        CancellationToken cancellationToken)
    {
        var definition = await _attributes.GetByIdWithValuesAsync(request.Id, cancellationToken);
        if (definition is null)
            return Result.Failure<ProductAttributeDto>(Error.NotFound("ProductAttributeDefinition", request.Id));

        return Result.Success(Map(definition));
    }

    internal static ProductAttributeDto Map(ProductAttributeDefinition d) => new(
        d.Id,
        d.Code,
        d.Name,
        d.Values.OrderBy(v => v.SortOrder).Select(v => new ProductAttributeValueDto(v.Id, v.Code, v.Name)).ToList());
}

public sealed record UpdateProductAttributeCommand(Guid Id, string Name, int SortOrder = 0)
    : IRequest<Result<ProductAttributeDto>>;

public sealed class UpdateProductAttributeCommandHandler
    : IRequestHandler<UpdateProductAttributeCommand, Result<ProductAttributeDto>>
{
    private readonly IProductAttributeRepository _attributes;
    private readonly ICurrentUser _currentUser;

    public UpdateProductAttributeCommandHandler(
        IProductAttributeRepository attributes,
        ICurrentUser currentUser)
    {
        _attributes = attributes;
        _currentUser = currentUser;
    }

    public async Task<Result<ProductAttributeDto>> Handle(
        UpdateProductAttributeCommand request,
        CancellationToken cancellationToken)
    {
        var definition = await _attributes.GetByIdWithValuesAsync(request.Id, cancellationToken);
        if (definition is null)
            return Result.Failure<ProductAttributeDto>(Error.NotFound("ProductAttributeDefinition", request.Id));

        var update = definition.Update(request.Name, request.SortOrder);
        if (update.IsFailure)
            return Result.Failure<ProductAttributeDto>(update.Error);

        definition.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);
        await _attributes.UpdateAsync(definition, cancellationToken);
        return Result.Success(GetProductAttributeByIdQueryHandler.Map(definition));
    }
}

public sealed record AddProductAttributeValueCommand(
    Guid DefinitionId,
    string Code,
    string Name,
    int SortOrder = 0) : IRequest<Result<Guid>>;

public sealed class AddProductAttributeValueCommandHandler
    : IRequestHandler<AddProductAttributeValueCommand, Result<Guid>>
{
    private readonly IProductAttributeRepository _attributes;
    private readonly ICurrentUser _currentUser;

    public AddProductAttributeValueCommandHandler(
        IProductAttributeRepository attributes,
        ICurrentUser currentUser)
    {
        _attributes = attributes;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(
        AddProductAttributeValueCommand request,
        CancellationToken cancellationToken)
    {
        var definition = await _attributes.GetByIdWithValuesAsync(request.DefinitionId, cancellationToken);
        if (definition is null)
            return Result.Failure<Guid>(Error.NotFound("ProductAttributeDefinition", request.DefinitionId));

        var added = definition.AddValue(request.Code, request.Name, request.SortOrder);
        if (added.IsFailure)
            return Result.Failure<Guid>(added.Error);

        definition.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);
        await _attributes.UpdateAsync(definition, cancellationToken);
        return Result.Success(added.Value.Id);
    }
}

public sealed record UpdateProductAttributeValueCommand(
    Guid DefinitionId,
    Guid ValueId,
    string Name,
    int SortOrder = 0) : IRequest<Result>;

public sealed class UpdateProductAttributeValueCommandHandler
    : IRequestHandler<UpdateProductAttributeValueCommand, Result>
{
    private readonly IProductAttributeRepository _attributes;
    private readonly ICurrentUser _currentUser;

    public UpdateProductAttributeValueCommandHandler(
        IProductAttributeRepository attributes,
        ICurrentUser currentUser)
    {
        _attributes = attributes;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(
        UpdateProductAttributeValueCommand request,
        CancellationToken cancellationToken)
    {
        var definition = await _attributes.GetByIdWithValuesAsync(request.DefinitionId, cancellationToken);
        if (definition is null)
            return Result.Failure(Error.NotFound("ProductAttributeDefinition", request.DefinitionId));

        var value = definition.FindValue(request.ValueId);
        if (value is null)
            return Result.Failure(Error.NotFound("ProductAttributeValue", request.ValueId));

        var update = value.Update(request.Name, request.SortOrder);
        if (update.IsFailure)
            return update;

        definition.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);
        await _attributes.UpdateAsync(definition, cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteProductAttributeValueCommand(Guid DefinitionId, Guid ValueId) : IRequest<Result>;

public sealed class DeleteProductAttributeValueCommandHandler
    : IRequestHandler<DeleteProductAttributeValueCommand, Result>
{
    private readonly IProductAttributeRepository _attributes;

    public DeleteProductAttributeValueCommandHandler(IProductAttributeRepository attributes)
    {
        _attributes = attributes;
    }

    public async Task<Result> Handle(
        DeleteProductAttributeValueCommand request,
        CancellationToken cancellationToken)
    {
        if (await _attributes.IsValueInUseAsync(request.ValueId, cancellationToken))
            return Result.Failure(Error.Validation("ValueId",
                "Cette valeur est utilisée par des variantes produit et ne peut pas être supprimée."));

        var definition = await _attributes.GetByIdWithValuesAsync(request.DefinitionId, cancellationToken);
        if (definition is null)
            return Result.Failure(Error.NotFound("ProductAttributeDefinition", request.DefinitionId));

        var remove = definition.RemoveValue(request.ValueId);
        if (remove.IsFailure)
            return remove;

        await _attributes.UpdateAsync(definition, cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteProductAttributeCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteProductAttributeCommandHandler
    : IRequestHandler<DeleteProductAttributeCommand, Result>
{
    private readonly IProductAttributeRepository _attributes;

    public DeleteProductAttributeCommandHandler(IProductAttributeRepository attributes)
    {
        _attributes = attributes;
    }

    public async Task<Result> Handle(
        DeleteProductAttributeCommand request,
        CancellationToken cancellationToken)
    {
        if (await _attributes.IsDefinitionInUseAsync(request.Id, cancellationToken))
            return Result.Failure(Error.Validation("Id",
                "Cet attribut est utilisé dans une matrice de variantes et ne peut pas être supprimé."));

        var definition = await _attributes.GetByIdWithValuesAsync(request.Id, cancellationToken);
        if (definition is null)
            return Result.Failure(Error.NotFound("ProductAttributeDefinition", request.Id));

        if (definition.Values.Any())
        {
            foreach (var value in definition.Values)
            {
                if (await _attributes.IsValueInUseAsync(value.Id, cancellationToken))
                    return Result.Failure(Error.Validation("Id",
                        "Une valeur de cet attribut est liée à des SKU variantes. Supprimez les variantes d'abord."));
            }
        }

        await _attributes.DeleteAsync(definition, cancellationToken);
        return Result.Success();
    }
}
