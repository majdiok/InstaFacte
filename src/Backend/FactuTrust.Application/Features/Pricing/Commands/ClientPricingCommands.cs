using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Pricing;
using FactuTrust.Domain.ValueObjects;
using MediatR;

namespace FactuTrust.Application.Features.Pricing.Commands;

// ─────────────────── Affectation d'une grille à un client ───────────────────

/// <summary>
/// Affecte une grille à un client, ou la retire (<paramref name="PriceListId"/> nul), auquel
/// cas le client revient au tarif catalogue.
/// </summary>
public sealed record AssignClientPriceListCommand(Guid ClientId, Guid? PriceListId) : IRequest<Result>;

public sealed class AssignClientPriceListCommandHandler
    : IRequestHandler<AssignClientPriceListCommand, Result>
{
    private readonly IClientRepository _clientRepository;
    private readonly IPriceListRepository _priceListRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public AssignClientPriceListCommandHandler(
        IClientRepository clientRepository,
        IPriceListRepository priceListRepository,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _clientRepository = clientRepository;
        _priceListRepository = priceListRepository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(AssignClientPriceListCommand request, CancellationToken cancellationToken)
    {
        var client = await _clientRepository.GetByIdAsync(request.ClientId, cancellationToken);
        if (client is null)
            return Result.Failure(Error.NotFound("Client", request.ClientId));

        // Affecter une grille inexistante ferait retomber le client au catalogue sans le dire :
        // on refuse plutôt que de laisser une affectation morte en base.
        if (request.PriceListId is { } priceListId)
        {
            if (!await _priceListRepository.ExistsAsync(priceListId, cancellationToken))
                return Result.Failure(Error.NotFound("Grille tarifaire", priceListId));
        }

        var previous = client.PriceListId;
        client.AssignPriceList(request.PriceListId);
        client.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);

        await _clientRepository.UpdateAsync(client, cancellationToken);

        await _auditService.LogAsync(
            "Client.PriceListAssigned", "Client", client.Id,
            oldValues: new { PriceListId = previous },
            newValues: new { request.PriceListId },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}

// ─────────────────── Prix négocié client / produit ───────────────────

public sealed record UpsertClientProductPriceCommand(
    Guid ClientId,
    Guid ProductId,
    decimal UnitPriceHT,
    DateTime? ValidFrom,
    DateTime? ValidUntil,
    bool IsActive) : IRequest<Result<Guid>>;

public sealed class UpsertClientProductPriceCommandHandler
    : IRequestHandler<UpsertClientProductPriceCommand, Result<Guid>>
{
    private readonly IClientProductPriceRepository _repository;
    private readonly IClientRepository _clientRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public UpsertClientProductPriceCommandHandler(
        IClientProductPriceRepository repository,
        IClientRepository clientRepository,
        IProductRepository productRepository,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _repository = repository;
        _clientRepository = clientRepository;
        _productRepository = productRepository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result<Guid>> Handle(
        UpsertClientProductPriceCommand request, CancellationToken cancellationToken)
    {
        if (!await _clientRepository.ExistsAsync(request.ClientId, cancellationToken))
            return Result.Failure<Guid>(Error.NotFound("Client", request.ClientId));

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
            return Result.Failure<Guid>(Error.NotFound("Produit", request.ProductId));

        var userId = _currentUser.UserId?.ToString() ?? "system";

        // Un seul prix négocié par couple client / produit — contrainte d'unicité en base. On met
        // donc à jour l'existant plutôt que d'échouer sur un doublon.
        var existing = await _repository.GetForClientProductAsync(
            request.ClientId, request.ProductId, cancellationToken);

        if (existing is not null)
        {
            var update = existing.UpdatePrice(Money.Create(request.UnitPriceHT));
            if (update.IsFailure)
                return Result.Failure<Guid>(update.Error);

            var validity = existing.SetValidity(request.ValidFrom, request.ValidUntil);
            if (validity.IsFailure)
                return Result.Failure<Guid>(validity.Error);

            if (request.IsActive)
                existing.Activate();
            else
                existing.Deactivate();

            existing.SetAuditInfo(userId, isUpdate: true);
            await _repository.UpdateAsync(existing, cancellationToken);

            await _auditService.LogAsync(
                "ClientProductPrice.Updated", "ClientProductPrice", existing.Id,
                newValues: new { request.ClientId, request.ProductId, request.UnitPriceHT, request.IsActive },
                cancellationToken: cancellationToken);

            return Result.Success(existing.Id);
        }

        var createResult = ClientProductPrice.Create(
            request.ClientId, request.ProductId, Money.Create(request.UnitPriceHT),
            request.ValidFrom, request.ValidUntil);

        if (createResult.IsFailure)
            return Result.Failure<Guid>(createResult.Error);

        var price = createResult.Value;
        if (!request.IsActive)
            price.Deactivate();

        price.SetAuditInfo(userId);
        await _repository.AddAsync(price, cancellationToken);

        await _auditService.LogAsync(
            "ClientProductPrice.Created", "ClientProductPrice", price.Id,
            newValues: new { request.ClientId, request.ProductId, request.UnitPriceHT, product.Code },
            cancellationToken: cancellationToken);

        return Result.Success(price.Id);
    }
}

public sealed record DeleteClientProductPriceCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteClientProductPriceCommandHandler
    : IRequestHandler<DeleteClientProductPriceCommand, Result>
{
    private readonly IClientProductPriceRepository _repository;
    private readonly IAuditService _auditService;

    public DeleteClientProductPriceCommandHandler(
        IClientProductPriceRepository repository, IAuditService auditService)
    {
        _repository = repository;
        _auditService = auditService;
    }

    public async Task<Result> Handle(DeleteClientProductPriceCommand request, CancellationToken cancellationToken)
    {
        var price = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (price is null)
            return Result.Failure(Error.NotFound("Prix négocié", request.Id));

        await _repository.DeleteAsync(price, cancellationToken);

        await _auditService.LogAsync(
            "ClientProductPrice.Deleted", "ClientProductPrice", request.Id,
            oldValues: new { price.ClientId, price.ProductId, price.UnitPriceHT.Amount },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
