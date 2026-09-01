namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Plan §3.2 (v2 stub) — future lookup against the Tunisian national registry (RNE) to validate a
/// NIF and fetch the associated legal identity before/at registration. This phase only ships a
/// no-op implementation (<c>NoOpNifRegistryLookupService</c> in Infrastructure), gated by
/// <c>Features:NifRegistryLookup:Enabled</c> (default <c>false</c>). No endpoint currently calls
/// this — it exists so a future RNE integration can be plugged in behind the same contract without
/// touching call sites. The frontend must keep doing its own client-side NIF format/coherence
/// checks; nothing here is wired into <c>POST /api/auth/register</c> yet.
/// </summary>
public interface INifRegistryLookupService
{
    /// <summary>
    /// Looks up <paramref name="nif"/> against the official registry. Returns
    /// <see cref="NifRegistryLookupResult.IsAvailable"/> = <c>false</c> whenever the lookup could
    /// not actually be performed (feature disabled, no registry configured, or a future real
    /// implementation's transient failure) — callers must treat an unavailable lookup as "no
    /// information", never as "NIF invalid".
    /// </summary>
    Task<NifRegistryLookupResult> LookupAsync(string nif, CancellationToken cancellationToken);
}

/// <summary>
/// Result of a NIF registry lookup. <see cref="IsAvailable"/> distinguishes "the lookup ran but
/// found nothing" (<see cref="Found"/> = false) from "the lookup could not run at all" (both false).
/// </summary>
public sealed record NifRegistryLookupResult(
    bool IsAvailable,
    bool Found,
    string? RegisteredCompanyName = null,
    char? TaxpayerCategory = null)
{
    public static NifRegistryLookupResult Unavailable { get; } = new(IsAvailable: false, Found: false);
}
