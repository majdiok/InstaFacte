using FactuTrust.Application.Configuration;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.CashDesk.Queries;

public sealed record CashDeskFeatureFlagsDto
{
    /// <summary>
    /// Reflète <c>AccountingSettings.CashDeskVatEnabled</c> : quand <c>true</c>, la saisie du taux
    /// de TVA sur les encaissements « ventes au comptant » est acceptée par l'API et le sélecteur
    /// correspondant doit être affiché côté frontend.
    /// </summary>
    public bool VatEnabled { get; init; }
}

public sealed record GetCashDeskFeatureFlagsQuery : IRequest<CashDeskFeatureFlagsDto>;

public sealed class GetCashDeskFeatureFlagsQueryHandler : IRequestHandler<GetCashDeskFeatureFlagsQuery, CashDeskFeatureFlagsDto>
{
    private readonly AccountingSettings _settings;

    public GetCashDeskFeatureFlagsQueryHandler(IOptions<AccountingSettings> settings)
    {
        _settings = settings.Value;
    }

    public Task<CashDeskFeatureFlagsDto> Handle(GetCashDeskFeatureFlagsQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(new CashDeskFeatureFlagsDto
        {
            VatEnabled = _settings.CashDeskVatEnabled
        });
}
