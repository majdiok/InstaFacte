using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Clients.Queries;

/// <summary>
/// Query to get client details by ID.
/// </summary>
public sealed record GetClientByIdQuery(Guid Id) : IRequest<Result<ClientDetailDto>>;

/// <summary>
/// Handler for GetClientByIdQuery.
/// </summary>
public sealed class GetClientByIdQueryHandler : IRequestHandler<GetClientByIdQuery, Result<ClientDetailDto>>
{
    private readonly IClientRepository _clientRepository;

    public GetClientByIdQueryHandler(IClientRepository clientRepository)
    {
        _clientRepository = clientRepository;
    }

    /// <summary>Kept for call sites that still reference the handler static.</summary>
    public static string GetClientCode(Guid id) => ClientDtoMapper.GetClientCode(id);

    public async Task<Result<ClientDetailDto>> Handle(GetClientByIdQuery request, CancellationToken cancellationToken)
    {
        var client = await _clientRepository.GetByIdAsync(request.Id, cancellationToken);
        if (client is null)
            return Result.Failure<ClientDetailDto>(Error.NotFound("Client", request.Id));

        return Result.Success(ClientDtoMapper.ToDetailDto(client));
    }
}
