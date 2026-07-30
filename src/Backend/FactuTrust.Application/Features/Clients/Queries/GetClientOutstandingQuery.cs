using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Clients.Queries;

/// <summary>
/// Encours d'un client. Destiné à l'affichage : fiche client, création de devis, confirmation
/// de commande.
///
/// ⚠️ Aucun appelant ne doit refuser une opération sur la base de <c>IsOverLimit</c> — décision
/// produit actée : le plafond avertit, il ne bloque pas.
/// </summary>
public sealed record GetClientOutstandingQuery(Guid ClientId) : IRequest<Result<ClientOutstandingDto>>;

public sealed class GetClientOutstandingQueryHandler
    : IRequestHandler<GetClientOutstandingQuery, Result<ClientOutstandingDto>>
{
    private readonly IClientOutstandingService _outstandingService;

    public GetClientOutstandingQueryHandler(IClientOutstandingService outstandingService)
    {
        _outstandingService = outstandingService;
    }

    public Task<Result<ClientOutstandingDto>> Handle(
        GetClientOutstandingQuery request, CancellationToken cancellationToken) =>
        _outstandingService.GetOutstandingAsync(request.ClientId, cancellationToken);
}
