using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

public sealed class FirmFiscalOpsFeature : IFirmFiscalOpsFeature
{
    private readonly FirmFiscalOpsOptions _options;

    public FirmFiscalOpsFeature(IOptions<FirmFiscalOpsOptions> options)
    {
        _options = options.Value;
    }

    public bool IsEnabled => _options.Enabled;
}
