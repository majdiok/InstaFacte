using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

public sealed class FirmGovernanceFeature : IFirmGovernanceFeature
{
    private readonly FirmGovernanceOptions _options;

    public FirmGovernanceFeature(IOptions<FirmGovernanceOptions> options)
    {
        _options = options.Value;
    }

    public bool IsEnabled => _options.Enabled;
}
