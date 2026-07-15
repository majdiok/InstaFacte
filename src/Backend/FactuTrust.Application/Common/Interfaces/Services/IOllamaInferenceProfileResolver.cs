using FactuTrust.Application.Features.AI;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IOllamaInferenceProfileResolver
{
    Task<OllamaInferenceProfile> ResolveForPlatformAsync(CancellationToken cancellationToken = default);

    OllamaInferenceProfile ResolveForDevice(OllamaInferenceDevice device);
}