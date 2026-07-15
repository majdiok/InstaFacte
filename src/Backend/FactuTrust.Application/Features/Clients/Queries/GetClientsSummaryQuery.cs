using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using MediatR;

namespace FactuTrust.Application.Features.Clients.Queries;

/// <summary>
/// Query to get aggregated totals for the client list over the ENTIRE filtered set.
/// Mirrors <see cref="GetClientsQuery"/> filters (without pagination) so the UI totals zone
/// reflects exactly the active filters, not just the current page.
/// </summary>
public sealed record GetClientsSummaryQuery(
    string? Search = null,
    ClientType? Type = null,
    bool? IsActive = null,
    string? Governorate = null) : IRequest<ClientListSummaryDto>;

/// <summary>
/// Handler for GetClientsSummaryQuery.
/// </summary>
public sealed class GetClientsSummaryQueryHandler
    : IRequestHandler<GetClientsSummaryQuery, ClientListSummaryDto>
{
    private readonly IClientRepository _clientRepository;

    public GetClientsSummaryQueryHandler(IClientRepository clientRepository)
    {
        _clientRepository = clientRepository;
    }

    public Task<ClientListSummaryDto> Handle(GetClientsSummaryQuery request, CancellationToken cancellationToken)
        => _clientRepository.GetSummaryAsync(
            request.Search,
            request.Type,
            request.IsActive,
            request.Governorate,
            cancellationToken);
}
