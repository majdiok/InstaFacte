using FactuTrust.Application.Configuration;
using FactuTrust.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

public sealed class AccountingFirmsFeature : IAccountingFirmsFeature
{
    private readonly AccountingFirmsOptions _options;

    public AccountingFirmsFeature(IOptions<AccountingFirmsOptions> options)
    {
        _options = options.Value;
    }

    public bool IsEnabled => _options.Enabled;
}
