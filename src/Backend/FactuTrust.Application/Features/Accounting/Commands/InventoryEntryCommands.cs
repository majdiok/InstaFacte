using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Commands;

// ── Catalogue des types d'inventaire (comptes par défaut pour préremplir l'UI) ──────

public sealed record GetInventoryEntryKindsQuery : IRequest<Result<IReadOnlyList<InventoryEntryKindDto>>>;

public sealed class GetInventoryEntryKindsQueryHandler
    : IRequestHandler<GetInventoryEntryKindsQuery, Result<IReadOnlyList<InventoryEntryKindDto>>>
{
    public Task<Result<IReadOnlyList<InventoryEntryKindDto>>> Handle(GetInventoryEntryKindsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<InventoryEntryKindDto> dtos = InventoryEntryDefaults.All
            .Select(d => new InventoryEntryKindDto
            {
                Kind = (int)d.Kind,
                Label = d.Label,
                DefaultDebitAccount = d.DefaultDebitAccount,
                DefaultCreditAccount = d.DefaultCreditAccount,
                AutoReverse = d.AutoReverse,
                Hint = d.Hint
            })
            .ToList();
        return Task.FromResult(Result.Success(dtos));
    }
}

// ── Création d'une écriture d'inventaire assistée ────────────────────────────────────

public sealed record CreateAssistedInventoryEntryCommand(CreateInventoryEntryRequest Request)
    : IRequest<Result<InventoryEntryResultDto>>;

public sealed class CreateAssistedInventoryEntryCommandHandler
    : IRequestHandler<CreateAssistedInventoryEntryCommand, Result<InventoryEntryResultDto>>
{
    private readonly IAssistedInventoryEntryService _service;

    public CreateAssistedInventoryEntryCommandHandler(IAssistedInventoryEntryService service)
    {
        _service = service;
    }

    public Task<Result<InventoryEntryResultDto>> Handle(CreateAssistedInventoryEntryCommand request, CancellationToken cancellationToken)
        => _service.CreateAsync(request.Request, cancellationToken);
}
