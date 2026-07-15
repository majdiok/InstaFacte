using System.ComponentModel.DataAnnotations;
using FactuTrust.Domain.Billing;

namespace FactuTrust.Application.DTOs;

/// <summary>Lot C1 — Vue admin d'un plan plateforme + ses limites/features/modules.</summary>
public sealed record PlanDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public bool IsPublic { get; init; }
    public bool IsActive { get; init; }
    public BillingPeriod BillingPeriod { get; init; }
    public string BillingPeriodDisplay { get; init; } = null!;
    public decimal BasePriceTND { get; init; }
    public string Currency { get; init; } = "TND";
    public int TrialDays { get; init; }
    public int SortOrder { get; init; }
    public DateTime? ArchivedAt { get; init; }
    public IReadOnlyList<PlanLimitDto> Limits { get; init; } = Array.Empty<PlanLimitDto>();
    public IReadOnlyList<PlanFeatureDto> Features { get; init; } = Array.Empty<PlanFeatureDto>();
    public IReadOnlyList<PlanModuleDto> Modules { get; init; } = Array.Empty<PlanModuleDto>();
    /// <summary>Nombre de souscriptions actuellement attachées à ce plan (lecture).</summary>
    public int SubscriptionsCount { get; init; }
}

public sealed record PlanLimitDto
{
    public string Key { get; init; } = null!;
    public string Value { get; init; } = null!;
}

public sealed record PlanFeatureDto
{
    public string FeatureKey { get; init; } = null!;
    public bool Enabled { get; init; }
}

public sealed record PlanModuleDto
{
    public int Module { get; init; }
    public string ModuleDisplay { get; init; } = null!;
    public bool IsIncluded { get; init; }
}

public sealed record CreatePlanRequest
{
    [Required, StringLength(50, MinimumLength = 2)]
    public string Code { get; init; } = null!;
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; init; } = null!;
    [StringLength(500)]
    public string? Description { get; init; }
    public BillingPeriod BillingPeriod { get; init; }
    [Range(0, 1_000_000)]
    public decimal BasePriceTND { get; init; }
    public bool IsPublic { get; init; } = true;
    [Range(0, 365)]
    public int TrialDays { get; init; } = 0;
    public int SortOrder { get; init; } = 0;
    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; init; } = "TND";
    public IReadOnlyList<PlanLimitDto> Limits { get; init; } = Array.Empty<PlanLimitDto>();
    public IReadOnlyList<PlanFeatureDto> Features { get; init; } = Array.Empty<PlanFeatureDto>();
    public IReadOnlyList<PlanModuleDto> Modules { get; init; } = Array.Empty<PlanModuleDto>();
}

public sealed record UpdatePlanRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; init; } = null!;
    [StringLength(500)]
    public string? Description { get; init; }
    public BillingPeriod BillingPeriod { get; init; }
    [Range(0, 1_000_000)]
    public decimal BasePriceTND { get; init; }
    public bool IsPublic { get; init; } = true;
    [Range(0, 365)]
    public int TrialDays { get; init; } = 0;
    public int SortOrder { get; init; } = 0;
    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; init; } = "TND";
    public IReadOnlyList<PlanLimitDto> Limits { get; init; } = Array.Empty<PlanLimitDto>();
    public IReadOnlyList<PlanFeatureDto> Features { get; init; } = Array.Empty<PlanFeatureDto>();
    public IReadOnlyList<PlanModuleDto> Modules { get; init; } = Array.Empty<PlanModuleDto>();
}

public sealed record ClonePlanRequest
{
    [Required, StringLength(50, MinimumLength = 2)]
    public string NewCode { get; init; } = null!;
    [Required, StringLength(100, MinimumLength = 2)]
    public string NewName { get; init; } = null!;
}

// ----- Tenant module overrides ---------------------------------------------

/// <summary>Lot C1 — Vue admin d'un override de module.</summary>
public sealed record TenantModuleOverrideDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public int Module { get; init; }
    public string ModuleDisplay { get; init; } = null!;
    public bool IsEnabled { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public Guid GrantedByUserId { get; init; }
    public string? Reason { get; init; }
    public bool IsCurrentlyActive { get; init; }
}

public sealed record SetTenantModuleOverrideRequest
{
    [Range(0, 100)]
    public int Module { get; init; }
    public bool IsEnabled { get; init; }
    public DateTime? ExpiresAt { get; init; }
    [StringLength(300)]
    public string? Reason { get; init; }
}
