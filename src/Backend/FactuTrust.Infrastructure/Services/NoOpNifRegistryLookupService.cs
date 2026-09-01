using FactuTrust.Application.Common.Interfaces;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Plan §3.2 (v2 stub) — no-op implementation of <see cref="INifRegistryLookupService"/>. Always
/// returns <see cref="NifRegistryLookupResult.Unavailable"/>: no RNE integration exists yet, and
/// this must never be mistaken for "the NIF is invalid". Swap this registration for a real
/// implementation once the RNE integration ships; the <c>Features:NifRegistryLookup</c> section
/// (<c>NifRegistryLookupOptions</c>) is already wired for that.
/// </summary>
public sealed class NoOpNifRegistryLookupService : INifRegistryLookupService
{
    public Task<NifRegistryLookupResult> LookupAsync(string nif, CancellationToken cancellationToken)
        => Task.FromResult(NifRegistryLookupResult.Unavailable);
}
