using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.SectorConfiguration;

/// <summary>
/// Structured result of <c>IRegistrationSectorService.ApplyModuleSelectionAsync</c> — plan §1.1/§1.2
/// (no silent rejections). Lets <c>AuthController.Register</c> surface, as non-blocking warnings,
/// what happened to the client's requested module selection instead of the caller having no way to
/// know that some of it was dropped or ignored entirely.
/// </summary>
public sealed record ModuleSelectionOutcome
{
    /// <summary>Final enabled module set actually granted (informational — grant rows are the source of truth).</summary>
    public IReadOnlyList<AppModule> EnabledModules { get; init; } = Array.Empty<AppModule>();

    /// <summary>
    /// Modules the client explicitly requested that did NOT make it into <see cref="EnabledModules"/>
    /// because the subscription plan does not include them (no-escalation ceiling).
    /// </summary>
    public IReadOnlyList<AppModule> DeniedByPlan { get; init; } = Array.Empty<AppModule>();

    /// <summary>Raw ids from the request that were dropped (undefined enum values, or Honoraires — firm-native, never offered at registration).</summary>
    public IReadOnlyList<int> DroppedInvalidIds { get; init; } = Array.Empty<int>();

    /// <summary>True when the kill-switch (<c>Features:RegistrationSector:Enabled=false</c>) was off but the client still sent a non-empty selection — the selection was entirely ignored (legacy all-modules behavior applied instead).</summary>
    public bool Ignored { get; init; }

    public static readonly ModuleSelectionOutcome Empty = new();

    public static readonly ModuleSelectionOutcome IgnoredKillSwitch = new() { Ignored = true };
}
