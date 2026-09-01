namespace FactuTrust.Application.Configuration;

/// <summary>
/// Plan §3.2 (v2 stub) — future RNE (Registre National des Entreprises) NIF lookup kill-switch.
/// Only a no-op implementation ships in this phase (see <c>INifRegistryLookupService</c>); this
/// flag exists so ops can flip integration on later without a code change once a real
/// implementation is registered. Defaults to <c>false</c> — no behavior change today.
/// </summary>
public sealed class NifRegistryLookupOptions
{
    public const string SectionName = "Features:NifRegistryLookup";

    public bool Enabled { get; set; } = false;
}
