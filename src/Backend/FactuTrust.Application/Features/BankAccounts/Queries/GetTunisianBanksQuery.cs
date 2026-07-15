using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Banking;
using MediatR;

namespace FactuTrust.Application.Features.BankAccounts.Queries;

public sealed record GetTunisianBanksQuery : IRequest<IReadOnlyList<TunisianBankReferenceDto>>;

public sealed class GetTunisianBanksQueryHandler : IRequestHandler<GetTunisianBanksQuery, IReadOnlyList<TunisianBankReferenceDto>>
{
    public Task<IReadOnlyList<TunisianBankReferenceDto>> Handle(GetTunisianBanksQuery request, CancellationToken cancellationToken)
    {
        var list = TunisianBankCatalog.Banks
            .Select(b => new TunisianBankReferenceDto
            {
                Code = b.Code,
                Name = b.Name,
                DefaultSwiftBic = b.DefaultSwiftBic
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<TunisianBankReferenceDto>>(list);
    }
}
