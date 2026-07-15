using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Stock.Commands;

/// <summary>
/// Command to update an existing warehouse.
/// </summary>
public sealed record UpdateWarehouseCommand(
    Guid Id,
    string Name,
    string? Address,
    bool IsDefault) : IRequest<Result<Unit>>;

/// <summary>
/// Validator for UpdateWarehouseCommand.
/// </summary>
public sealed class UpdateWarehouseCommandValidator : AbstractValidator<UpdateWarehouseCommand>
{
    public UpdateWarehouseCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithMessage("Le nom de l'entrepôt est obligatoire (max 100 car.)");
    }
}

/// <summary>
/// Handler for UpdateWarehouseCommand.
/// </summary>
public sealed class UpdateWarehouseCommandHandler : IRequestHandler<UpdateWarehouseCommand, Result<Unit>>
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ITenantContext _tenantContext;

    public UpdateWarehouseCommandHandler(
        IWarehouseRepository warehouseRepository,
        ITenantContext tenantContext)
    {
        _warehouseRepository = warehouseRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Result<Unit>> Handle(UpdateWarehouseCommand request, CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is null)
            return Result.Failure<Unit>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var warehouse = await _warehouseRepository.GetByIdAsync(request.Id, cancellationToken);
        if (warehouse is null)
            return Result.Failure<Unit>(Error.NotFound("Warehouse", request.Id));

        warehouse.Update(request.Name, request.Address);

        // Handle default warehouse changes
        if (request.IsDefault && !warehouse.IsDefault)
        {
            var currentDefault = await _warehouseRepository.GetDefaultAsync(cancellationToken);
            if (currentDefault != null && currentDefault.Id != warehouse.Id)
            {
                currentDefault.RemoveDefault();
                await _warehouseRepository.UpdateAsync(currentDefault, cancellationToken);
            }
            warehouse.SetAsDefault();
        }
        else if (!request.IsDefault && warehouse.IsDefault)
        {
             // Prevent removing default if it's the only one or verify logic
             // Usually we require at least one default. For now, we allow it but maybe we should warn? 
             // Logic: Just remove default flag.
             warehouse.RemoveDefault();
        }

        await _warehouseRepository.UpdateAsync(warehouse, cancellationToken);

        return Result.Success(Unit.Value);
    }
}
