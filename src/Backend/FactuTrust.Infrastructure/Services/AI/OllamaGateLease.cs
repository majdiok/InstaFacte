using FactuTrust.Application.Common.Interfaces.Services;

namespace FactuTrust.Infrastructure.Services.AI;

public sealed class OllamaGateLease : IOllamaGateLease
{
    private SemaphoreSlim? _semaphore;

    public OllamaGateLease(SemaphoreSlim semaphore, long waitMilliseconds)
    {
        _semaphore = semaphore;
        WaitMilliseconds = Math.Max(0, waitMilliseconds);
    }

    public long WaitMilliseconds { get; }

    public void Dispose()
    {
        Interlocked.Exchange(ref _semaphore, null)?.Release();
    }
}