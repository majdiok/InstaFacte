using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Pricing;
using FactuTrust.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.Pricing.Commands;

// ───────────────────────────── Création ─────────────────────────────

public sealed record CreatePriceListCommand(
    string Name,
    string? Currency,
    DateTime? ValidFrom,
    DateTime? ValidUntil) : IRequest<Result<Guid>>;

public sealed class CreatePriceListCommandValidator : AbstractValidator<CreatePriceListCommand>
{
    public CreatePriceListCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100)
            .WithMessage("Le nom de la grille est obligatoire (100 caractères maximum)");
    }
}

public sealed class CreatePriceListCommandHandler
    : IRequestHandler<CreatePriceListCommand, Result<Guid>>
{
    private readonly IPriceListRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public CreatePriceListCommandHandler(
        IPriceListRepository repository, ICurrentUser currentUser, IAuditService auditService)
    {
        _repository = repository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result<Guid>> Handle(CreatePriceListCommand request, CancellationToken cancellationToken)
    {
        var result = PriceList.Create(request.Name, request.Currency ?? Money.DefaultCurrency,
            request.ValidFrom, request.ValidUntil);

        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        var priceList = result.Value;
        priceList.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");

        await _repository.AddAsync(priceList, cancellationToken);

        await _auditService.LogAsync(
            "PriceList.Created", "PriceList", priceList.Id,
            newValues: new { priceList.Name, priceList.Currency },
            cancellationToken: cancellationToken);

        return Result.Success(priceList.Id);
    }
}

// ───────────────────────────── Mise à jour ─────────────────────────────

public sealed record UpdatePriceListCommand(
    Guid Id,
    string Name,
    DateTime? ValidFrom,
    DateTime? ValidUntil,
    bool IsActive) : IRequest<Result>;

public sealed class UpdatePriceListCommandHandler
    : IRequestHandler<UpdatePriceListCommand, Result>
{
    private readonly IPriceListRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public UpdatePriceListCommandHandler(
        IPriceListRepository repository, ICurrentUser currentUser, IAuditService auditService)
    {
        _repository = repository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(UpdatePriceListCommand request, CancellationToken cancellationToken)
    {
        var priceList = await _repository.GetByIdWithItemsAsync(request.Id, cancellationToken);
        if (priceList is null)
            return Result.Failure(Error.NotFound("Grille tarifaire", request.Id));

        var rename = priceList.Rename(request.Name);
        if (rename.IsFailure)
            return rename;

        var validity = priceList.SetValidity(request.ValidFrom, request.ValidUntil);
        if (validity.IsFailure)
            return validity;

        // Désactiver une grille ne touche AUCUN document déjà émis : leur prix est figé.
        if (request.IsActive)
            priceList.Activate();
        else
            priceList.Deactivate();

        priceList.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");
        await _repository.UpdateAsync(priceList, cancellationToken);

        await _auditService.LogAsync(
            "PriceList.Updated", "PriceList", priceList.Id,
            newValues: new { priceList.Name, priceList.IsActive, priceList.ValidFrom, priceList.ValidUntil },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

// ───────────────────────────── Prix d'un produit ─────────────────────────────

public sealed record SetPriceListItemCommand(
    Guid PriceListId,
    Guid ProductId,
    decimal UnitPriceHT) : IRequest<Result>;

public sealed class SetPriceListItemCommandHandler
    : IRequestHandler<SetPriceListItemCommand, Result>
{
    private readonly IPriceListRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public SetPriceListItemCommandHandler(
        IPriceListRepository repository,
        IProductRepository productRepository,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _repository = repository;
        _productRepository = productRepository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(SetPriceListItemCommand request, CancellationToken cancellationToken)
    {
        var priceList = await _repository.GetByIdWithItemsAsync(request.PriceListId, cancellationToken);
        if (priceList is null)
            return Result.Failure(Error.NotFound("Grille tarifaire", request.PriceListId));

        // Le produit doit exister : une grille qui référence un produit fantôme donnerait un prix
        // que le résolveur ne pourrait jamais servir.
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
            return Result.Failure(Error.NotFound("Produit", request.ProductId));

        var setResult = priceList.SetPrice(request.ProductId, Money.Create(request.UnitPriceHT, priceList.Currency));
        if (setResult.IsFailure)
            return setResult;

        priceList.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");
        await _repository.UpdateAsync(priceList, cancellationToken);

        await _auditService.LogAsync(
            "PriceList.PriceSet", "PriceList", priceList.Id,
            newValues: new { request.ProductId, request.UnitPriceHT, product.Code },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record RemovePriceListItemCommand(Guid PriceListId, Guid ProductId) : IRequest<Result>;

public sealed class RemovePriceListItemCommandHandler
    : IRequestHandler<RemovePriceListItemCommand, Result>
{
    private readonly IPriceListRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public RemovePriceListItemCommandHandler(
        IPriceListRepository repository, ICurrentUser currentUser, IAuditService auditService)
    {
        _repository = repository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(RemovePriceListItemCommand request, CancellationToken cancellationToken)
    {
        var priceList = await _repository.GetByIdWithItemsAsync(request.PriceListId, cancellationToken);
        if (priceList is null)
            return Result.Failure(Error.NotFound("Grille tarifaire", request.PriceListId));

        priceList.RemovePrice(request.ProductId);

        priceList.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");
        await _repository.UpdateAsync(priceList, cancellationToken);

        await _auditService.LogAsync(
            "PriceList.PriceRemoved", "PriceList", priceList.Id,
            newValues: new { request.ProductId },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

// ───────────────────────────── Paliers quantitatifs ─────────────────────────────

public sealed record SetPriceListTierCommand(
    Guid PriceListId,
    Guid ProductId,
    decimal MinQuantity,
    decimal UnitPriceHT) : IRequest<Result>;

public sealed class SetPriceListTierCommandHandler
    : IRequestHandler<SetPriceListTierCommand, Result>
{
    private readonly IPriceListRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public SetPriceListTierCommandHandler(
        IPriceListRepository repository, ICurrentUser currentUser, IAuditService auditService)
    {
        _repository = repository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(SetPriceListTierCommand request, CancellationToken cancellationToken)
    {
        var priceList = await _repository.GetByIdWithItemsAsync(request.PriceListId, cancellationToken);
        if (priceList is null)
            return Result.Failure(Error.NotFound("Grille tarifaire", request.PriceListId));

        var result = priceList.SetTier(
            request.ProductId,
            request.MinQuantity,
            Money.Create(request.UnitPriceHT, priceList.Currency));

        if (result.IsFailure)
            return result;

        priceList.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");
        await _repository.UpdateAsync(priceList, cancellationToken);

        await _auditService.LogAsync(
            "PriceList.TierSet", "PriceList", priceList.Id,
            newValues: new { request.ProductId, request.MinQuantity, request.UnitPriceHT },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

public sealed record RemovePriceListTierCommand(
    Guid PriceListId,
    Guid ProductId,
    decimal MinQuantity) : IRequest<Result>;

public sealed class RemovePriceListTierCommandHandler
    : IRequestHandler<RemovePriceListTierCommand, Result>
{
    private readonly IPriceListRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public RemovePriceListTierCommandHandler(
        IPriceListRepository repository, ICurrentUser currentUser, IAuditService auditService)
    {
        _repository = repository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(RemovePriceListTierCommand request, CancellationToken cancellationToken)
    {
        var priceList = await _repository.GetByIdWithItemsAsync(request.PriceListId, cancellationToken);
        if (priceList is null)
            return Result.Failure(Error.NotFound("Grille tarifaire", request.PriceListId));

        priceList.RemoveTier(request.ProductId, request.MinQuantity);

        priceList.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");
        await _repository.UpdateAsync(priceList, cancellationToken);

        await _auditService.LogAsync(
            "PriceList.TierRemoved", "PriceList", priceList.Id,
            newValues: new { request.ProductId, request.MinQuantity },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

// ───────────────────────────── Suppression ─────────────────────────────

public sealed record DeletePriceListCommand(Guid Id) : IRequest<Result>;

public sealed class DeletePriceListCommandHandler
    : IRequestHandler<DeletePriceListCommand, Result>
{
    private readonly IPriceListRepository _repository;
    private readonly IAuditService _auditService;

    public DeletePriceListCommandHandler(
        IPriceListRepository repository,
        IAuditService auditService)
    {
        _repository = repository;
        _auditService = auditService;
    }

    public async Task<Result> Handle(DeletePriceListCommand request, CancellationToken cancellationToken)
    {
        var priceList = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (priceList is null)
            return Result.Failure(Error.NotFound("Grille tarifaire", request.Id));

        // Une grille encore affectée n'est pas supprimable : les clients concernés retomberaient
        // silencieusement au catalogue. On oriente vers la désactivation, qui est réversible.
        var assignedCount = await _repository.CountAssignedClientsAsync(request.Id, cancellationToken);
        if (assignedCount > 0)
        {
            return Result.Failure(Error.Validation("PriceListId",
                $"Cette grille est affectée à {assignedCount} client(s). Retirez l'affectation, " +
                "ou désactivez la grille plutôt que de la supprimer."));
        }

        await _repository.DeleteAsync(priceList, cancellationToken);

        await _auditService.LogAsync(
            "PriceList.Deleted", "PriceList", request.Id,
            oldValues: new { priceList.Name },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
