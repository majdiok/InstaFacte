using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.ValueObjects;

namespace FactuTrust.Infrastructure.Services;

public sealed class FiscalStampResolver : IFiscalStampResolver
{
    public const string FiscalStampAccountNumber = "4478";

    private readonly ITaxRepository _taxRepository;

    public FiscalStampResolver(ITaxRepository taxRepository)
    {
        _taxRepository = taxRepository;
    }

    public async Task<Money> ResolveSignedStampAsync(bool isCreditNote, CancellationToken cancellationToken = default)
    {
        var tax = await _taxRepository.GetActiveSalesStampTaxAsync(cancellationToken);
        if (tax is null)
            return Money.Zero();

        var abs = tax.Value;
        if (abs == 0)
            return Money.Zero();

        var signed = isCreditNote ? -abs : abs;
        return Money.FromSignedAmount(signed, Money.DefaultCurrency);
    }
}
