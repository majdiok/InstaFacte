using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Stock.Commands;

/// <summary>
/// Command to create a new warehouse.
/// </summary>
public sealed record CreateWarehouseCommand(
    string Code,
    string Name,
    string? Address,
    bool IsDefault) : IRequest<Result<Guid>>;

/// <summary>
/// Validator for CreateWarehouseCommand.
/// </summary>
public sealed class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20).WithMessage("Le code entrepôt est obligatoire (max 20 car.)");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithMessage("Le nom de l'entrepôt est obligatoire (max 100 car.)");
    }
}

/// <summary>
/// Handler for CreateWarehouseCommand.
/// </summary>
public sealed class CreateWarehouseCommandHandler : IRequestHandler<CreateWarehouseCommand, Result<Guid>>
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ITenantContext _tenantContext;

    public CreateWarehouseCommandHandler(
        IWarehouseRepository warehouseRepository,
        ITenantContext tenantContext)
    {
        _warehouseRepository = warehouseRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Result<Guid>> Handle(CreateWarehouseCommand request, CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is null)
            return Result.Failure<Guid>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        // Check for duplicate code
        var codeExists = await _warehouseRepository.CodeExistsAsync(request.Code, cancellationToken: cancellationToken);
        if (codeExists)
            return Result.Failure<Guid>(Error.Conflict("Un entrepôt existe déjà avec ce code."));

        // If setting as default, remove default from other warehouses
        if (request.IsDefault)
        {
            var currentDefault = await _warehouseRepository.GetDefaultAsync(cancellationToken);
            if (currentDefault != null)
            {
                currentDefault.RemoveDefault();
                await _warehouseRepository.UpdateAsync(currentDefault, cancellationToken);
            }
        }

        var warehouseResult = Warehouse.Create(
            request.Code,
            request.Name,
            request.Address,
            request.IsDefault);

        if (warehouseResult.IsFailure)
            return Result.Failure<Guid>(warehouseResult.Error);

        var warehouse = warehouseResult.Value;
        await _warehouseRepository.AddAsync(warehouse, cancellationToken);

        return Result.Success(warehouse.Id);
    }
}
