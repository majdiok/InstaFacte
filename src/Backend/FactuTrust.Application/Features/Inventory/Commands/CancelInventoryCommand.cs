using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Inventory.Commands;

/// <summary>
/// Annule un inventaire en cours sans appliquer de modifications au stock.
/// </summary>
public sealed record CancelInventoryCommand(
    Guid InventoryId) : IRequest<Result<CancelInventoryResult>>;

/// <summary>
/// Résultat de l'annulation.
/// </summary>
public sealed record CancelInventoryResult
{
    public Guid InventoryId { get; init; }
    public string HumanMessage { get; init; } = string.Empty;
}

public sealed class CancelInventoryCommandValidator : AbstractValidator<CancelInventoryCommand>
{
    public CancelInventoryCommandValidator()
    {
        RuleFor(x => x.InventoryId)
            .NotEmpty()
            .WithMessage("L'identifiant de l'inventaire est obligatoire.");
    }
}

public sealed class CancelInventoryCommandHandler : IRequestHandler<CancelInventoryCommand, Result<CancelInventoryResult>>
{
    private readonly IPhysicalInventoryRepository _inventoryRepository;
    private readonly ITenantContext _tenantContext;

    public CancelInventoryCommandHandler(
        IPhysicalInventoryRepository inventoryRepository,
        ITenantContext tenantContext)
    {
        _inventoryRepository = inventoryRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Result<CancelInventoryResult>> Handle(CancelInventoryCommand request, CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is null)
            return Result.Failure<CancelInventoryResult>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var inventory = await _inventoryRepository.GetWithLinesAsync(request.InventoryId, cancellationToken);
        if (inventory == null)
            return Result.Failure<CancelInventoryResult>(Error.NotFound("Inventaire", request.InventoryId));

        var cancelResult = inventory.Cancel();
        if (cancelResult.IsFailure)
            return Result.Failure<CancelInventoryResult>(cancelResult.Error);

        await _inventoryRepository.UpdateAsync(inventory, cancellationToken);

        return Result.Success(new CancelInventoryResult
        {
            InventoryId = inventory.Id,
            HumanMessage = "🚫 Inventaire annulé. Aucune modification n'a été apportée au stock."
        });
    }
}
