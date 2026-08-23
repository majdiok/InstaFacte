using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Inventory.Commands;

/// <summary>
/// Enregistre le comptage d'un produit pendant l'inventaire.
/// L'utilisateur indique simplement combien il a compté.
/// </summary>
public sealed record RecordCountCommand(
    Guid InventoryId,
    Guid ProductId,
    decimal CountedQuantity,
    Guid? ProductLotId = null) : IRequest<Result<RecordCountResult>>;

/// <summary>
/// Résultat du comptage avec message pédagogique.
/// </summary>
public sealed record RecordCountResult
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal TheoreticalQuantity { get; init; }
    public decimal CountedQuantity { get; init; }
    public decimal Difference { get; init; }
    public string HumanMessage { get; init; } = string.Empty;
    public int TotalProducts { get; init; }
    public int CountedProducts { get; init; }
    public bool IsInventoryComplete { get; init; }
}

public sealed class RecordCountCommandValidator : AbstractValidator<RecordCountCommand>
{
    public RecordCountCommandValidator()
    {
        RuleFor(x => x.InventoryId)
            .NotEmpty()
            .WithMessage("L'identifiant de l'inventaire est obligatoire.");

        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("L'identifiant du produit est obligatoire.");

        RuleFor(x => x.CountedQuantity)
            .GreaterThanOrEqualTo(0)
            .WithMessage("La quantité comptée ne peut pas être négative.");
    }
}

public sealed class RecordCountCommandHandler : IRequestHandler<RecordCountCommand, Result<RecordCountResult>>
{
    private readonly IPhysicalInventoryRepository _inventoryRepository;
    private readonly ITenantContext _tenantContext;

    public RecordCountCommandHandler(
        IPhysicalInventoryRepository inventoryRepository,
        ITenantContext tenantContext)
    {
        _inventoryRepository = inventoryRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Result<RecordCountResult>> Handle(RecordCountCommand request, CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is null)
            return Result.Failure<RecordCountResult>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        // Get inventory with lines
        var inventory = await _inventoryRepository.GetWithLinesAsync(request.InventoryId, cancellationToken);
        if (inventory == null)
            return Result.Failure<RecordCountResult>(Error.NotFound("Inventaire", request.InventoryId));

        var line = request.ProductLotId.HasValue
            ? inventory.CountLines.FirstOrDefault(l => l.ProductId == request.ProductId && l.ProductLotId == request.ProductLotId)
            : inventory.CountLines.FirstOrDefault(l => l.ProductId == request.ProductId && l.ProductLotId == null)
              ?? inventory.CountLines.FirstOrDefault(l => l.ProductId == request.ProductId);
        if (line == null)
            return Result.Failure<RecordCountResult>(Error.NotFound("Produit dans l'inventaire", request.ProductId));

        var recordResult = inventory.RecordCount(request.ProductId, request.CountedQuantity, request.ProductLotId);
        if (recordResult.IsFailure)
            return Result.Failure<RecordCountResult>(recordResult.Error);

        await _inventoryRepository.UpdateAsync(inventory, cancellationToken);

        // Generate pedagogical message
        var difference = request.CountedQuantity - line.TheoreticalQuantity;
        var humanMessage = GenerateHumanMessage(line.TheoreticalQuantity, request.CountedQuantity, difference);

        return Result.Success(new RecordCountResult
        {
            ProductId = request.ProductId,
            ProductName = line.ProductName,
            TheoreticalQuantity = line.TheoreticalQuantity,
            CountedQuantity = request.CountedQuantity,
            Difference = difference,
            HumanMessage = humanMessage,
            TotalProducts = inventory.TotalProducts,
            CountedProducts = inventory.CountedProducts,
            IsInventoryComplete = inventory.IsComplete
        });
    }

    private static string GenerateHumanMessage(decimal theoretical, decimal counted, decimal difference)
    {
        if (difference == 0)
            return "✅ Parfait ! Votre comptage correspond exactement.";

        var absDiff = Math.Abs(difference);
        var unitText = absDiff == 1 ? "unité" : "unités";

        if (difference > 0)
            return $"➕ Vous avez {absDiff} {unitText} de plus que prévu.";
        else
            return $"➖ Il vous manque {absDiff} {unitText}.";
    }
}
