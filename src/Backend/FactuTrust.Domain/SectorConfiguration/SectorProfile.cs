using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.SectorConfiguration;

/// <summary>
/// Resolved sector configuration for a (segment, domain) pair — plan §5/§6.1 B1.
/// Returned by <see cref="SectorConfigurationCatalog.Resolve"/> and consumed by
/// <c>IRegistrationSectorService</c> to drive module selection + default warehouse naming at
/// registration time.
/// </summary>
public sealed record SectorProfile
{
    public required string SegmentCode { get; init; }

    public string? DomainCode { get; init; }

    /// <summary>Always-enabled modules (Administration, Clients, Products, Sales, Treasury, Reports).</summary>
    public required IReadOnlyList<AppModule> CoreModules { get; init; }

    /// <summary>Pre-checked in the wizard (segment base ∪ domain overlay, core excluded).</summary>
    public required IReadOnlyList<AppModule> RecommendedModules { get; init; }

    /// <summary>Listed unchecked in the wizard (everything else except core/recommended/Honoraires).</summary>
    public required IReadOnlyList<AppModule> OptionalModules { get; init; }

    /// <summary>Used only when <c>RegisterDto.WarehouseName</c> is null/blank.</summary>
    public string? DefaultWarehouseName { get; init; }
}
