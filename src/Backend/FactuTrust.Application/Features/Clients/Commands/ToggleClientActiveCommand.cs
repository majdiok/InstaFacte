using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Clients.Queries;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Clients.Commands;

/// <summary>
/// Command to toggle client active status.
/// </summary>
public sealed record ToggleClientActiveCommand(Guid Id) : IRequest<Result<ClientDetailDto>>;

/// <summary>
/// Handler for ToggleClientActiveCommand.
/// </summary>
public sealed class ToggleClientActiveCommandHandler : IRequestHandler<ToggleClientActiveCommand, Result<ClientDetailDto>>
{
    private readonly IClientRepository _clientRepository;
    private readonly IMediator _mediator;

    public ToggleClientActiveCommandHandler(
        IClientRepository clientRepository,
        IMediator mediator)
    {
        _clientRepository = clientRepository;
        _mediator = mediator;
    }

    public async Task<Result<ClientDetailDto>> Handle(ToggleClientActiveCommand request, CancellationToken cancellationToken)
    {
        var client = await _clientRepository.GetByIdAsync(request.Id, cancellationToken);
        if (client is null)
            return Result.Failure<ClientDetailDto>(Error.NotFound("Client", request.Id));

        if (client.IsActive)
            client.Deactivate();
        else
            client.Reactivate();

        await _clientRepository.UpdateAsync(client, cancellationToken);

        return await _mediator.Send(new GetClientByIdQuery(request.Id), cancellationToken);
    }
}
