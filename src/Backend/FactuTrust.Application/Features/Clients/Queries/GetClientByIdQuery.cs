using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
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

    public static string GetClientCode(Guid id) => "CLI-" + id.ToString("N")[..8].ToUpperInvariant();

    public async Task<Result<ClientDetailDto>> Handle(GetClientByIdQuery request, CancellationToken cancellationToken)
    {
        var client = await _clientRepository.GetByIdAsync(request.Id, cancellationToken);
        if (client is null)
            return Result.Failure<ClientDetailDto>(Error.NotFound("Client", request.Id));

        var dto = new ClientDetailDto
        {
            Id = client.Id,
            Code = GetClientCode(client.Id),
            Name = client.Name,
            Type = client.Type,
            TypeDisplay = client.Type.ToDisplayString(),
            Nif = client.NIF?.Value,
            Address = new AddressDto
            {
                Street = client.Address.Street,
                StreetLine2 = client.Address.StreetLine2,
                City = client.Address.City,
                PostalCode = client.Address.PostalCode,
                Governorate = client.Address.Governorate,
                Country = client.Address.Country,
                FullAddress = client.Address.ToSingleLine()
            },
            Email = client.Email.Value,
            Phone = client.Phone?.Value,
            ContactPerson = client.ContactPerson,
            Notes = client.Notes,
            IsActive = client.IsActive,
            CreatedAt = client.CreatedAt,
            UpdatedAt = client.UpdatedAt
        };

        return Result.Success(dto);
    }
}
