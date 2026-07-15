using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

/// <summary>Tenant row for platform operator list.</summary>
public sealed record PlatformTenantListItemDto
{
    public Guid TenantId { get; init; }
    public string CompanyName { get; init; } = null!;
    public string CompanyEmail { get; init; } = null!;
    public string TaxRegimeDisplay { get; init; } = null!;
    public bool IsActive { get; init; }
    public string DatabaseName { get; init; } = null!;
    public SubscriptionPlan? SubscriptionPlan { get; init; }
    public string? SubscriptionPlanDisplay { get; init; }
    public SubscriptionStatus? SubscriptionStatus { get; init; }
    public string? SubscriptionStatusDisplay { get; init; }
    public DateTime? SubscriptionEndDate { get; init; }
    public bool IsPayingSubscriber { get; init; }

    // ----- Lot A2 additions (optionnels, rétro-compatibles) -----------------
    /// <summary>NIF / matricule fiscal de l'entreprise (affichage en mono dans la table).</summary>
    public string? Nif { get; init; }
    /// <summary>Date de création du tenant (pour tri "Inscription" et chips).</summary>
    public DateTime? CreatedAt { get; init; }
    /// <summary>
    /// Dernière activité : <c>UpdatedAt ?? CreatedAt</c> côté tenant pour l'instant
    /// (proxy honnête tant qu'aucun tracking d'activité réel n'est en place — Lot D).
    /// </summary>
    public DateTime? LastActivityAt { get; init; }
    /// <summary>
    /// MRR (Monthly Recurring Revenue) estimé en TND pour cette ligne :
    ///  - Monthly Active/Trial → <c>MonthlyPrice.Amount</c> (ou 49 TND par défaut)
    ///  - Annual Active/Trial → <c>AnnualPrice.Amount / 12</c> (ou 39 TND par défaut)
    ///  - Sinon → null
    /// </summary>
    public decimal? MrrTnd { get; init; }
}

/// <summary>Paged tenant list for platform operators.</summary>
public sealed record PlatformTenantListPageDto
{
    public IReadOnlyList<PlatformTenantListItemDto> Items { get; init; } = Array.Empty<PlatformTenantListItemDto>();
    public int TotalCount { get; init; }
}

/// <summary>Global tenant / subscription counts for platform dashboard KPIs.</summary>
public sealed record PlatformTenantStatsDto
{
    public int TotalTenants { get; init; }
    public int PayingSubscribers { get; init; }
    public int NonPayingSubscribers { get; init; }

    // ----- Lot A2 additions (optionnels, rétro-compatibles) -----------------
    /// <summary>Variation du nombre total d'entreprises sur les 30 derniers jours (en valeur absolue).</summary>
    public int? TotalDelta30d { get; init; }
    /// <summary>MRR estimé du parc, en TND, calculé via PlatformSubscriptionMetricsHelper.</summary>
    public decimal? MrrEstimateTnd { get; init; }
    /// <summary>
    /// Taux de conversion essai → payant sur les 30 derniers jours, exprimé en fraction (0..1).
    /// Formule : <c>convertedTrials30d / endedTrials30d</c>. Null si dénominateur = 0.
    /// </summary>
    public decimal? TrialConversionRate30d { get; init; }
    /// <summary>Variation du taux de conversion vs les 30 jours précédents (différence absolue de fraction).</summary>
    public decimal? TrialConversionDelta30d { get; init; }
    /// <summary>Nombre d'entreprises en risque (PastDue + Suspended).</summary>
    public int? RiskCount { get; init; }
    /// <summary>Série temporelle des nouveaux signups, 30 valeurs (J-29 à J-0).</summary>
    public IReadOnlyList<int>? NewSignupsTimeseries30d { get; init; }
}

/// <summary>Filters and pagination for <see cref="IPlatformTenantQueryService.ListAsync"/>.</summary>
public sealed record PlatformTenantListQuery
{
    public string? Search { get; init; }
    /// <summary>paying, non_paying, or all (default).</summary>
    public string? Segment { get; init; }
    public SubscriptionPlan? Plan { get; init; }
    public SubscriptionStatus? SubscriptionStatus { get; init; }
    public bool? IsActive { get; init; }
    public TaxRegime? TaxRegime { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;

    // ----- Lot A2 additions (optionnels, rétro-compatibles) -----------------
    /// <summary>
    /// Champ de tri serveur. Valeurs reconnues :
    ///   <c>name</c> (par défaut, alphabétique sur CompanyName) · <c>createdAt</c> ·
    ///   <c>lastActivity</c> · <c>plan</c> · <c>status</c> · <c>endDate</c> · <c>mrr</c>.
    /// Toute autre valeur retombe sur le tri par nom.
    /// </summary>
    public string? SortBy { get; init; }
    /// <summary>Direction du tri : <c>asc</c> (défaut) ou <c>desc</c>.</summary>
    public string? SortDir { get; init; }
}

/// <summary>Tenant detail for platform operators.</summary>
public sealed record PlatformTenantDetailDto
{
    public Guid TenantId { get; init; }
    public string CompanyName { get; init; } = null!;
    public string CompanyEmail { get; init; } = null!;
    public string Nif { get; init; } = null!;
    public string Phone { get; init; } = null!;
    public string City { get; init; } = null!;
    public string Governorate { get; init; } = null!;
    public string? Website { get; init; }
    public string TaxRegimeDisplay { get; init; } = null!;
    public bool IsActive { get; init; }
    public DateTime? DeactivatedAt { get; init; }
    public string DatabaseName { get; init; } = null!;
    public SubscriptionPlan? SubscriptionPlan { get; init; }
    public string? SubscriptionPlanDisplay { get; init; }
    public SubscriptionStatus? SubscriptionStatus { get; init; }
    public string? SubscriptionStatusDisplay { get; init; }
    public DateTime? SubscriptionEndDate { get; init; }
    public bool IsPayingSubscriber { get; init; }

    /// <summary>Filled by the API layer after checking tenant database migrations.</summary>
    public bool HasMigrationsApplied { get; init; }
}

/// <summary>Result of a bulk tenant migration operation.</summary>
public sealed record MigrationResultDto
{
    public int TotalTenants { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
}

/// <summary>
/// Aggregated KPIs for the platform Migrations page (Lot A3).
/// "Failures24h" reste à 0 tant que le tracking persistant des exécutions
/// n'est pas en place (Lot D4 : MigrationRuns + MigrationRunItems).
/// </summary>
public sealed record MigrationStatsDto
{
    /// <summary>Nombre total de tenants actifs.</summary>
    public int TotalTenants { get; init; }
    /// <summary>Tenants à jour (toutes les migrations EF appliquées).</summary>
    public int UpToDate { get; init; }
    /// <summary>Tenants en retard (au moins une migration manquante).</summary>
    public int Pending { get; init; }
    /// <summary>Échecs survenus dans les dernières 24h. Toujours 0 tant que le tracking persistant n'existe pas.</summary>
    public int Failures24h { get; init; }
}

/// <summary>
/// Aggregated KPIs for the platform Vitrines 3D page (Lot A4).
/// "Refusées" = vitrines en statut Draft avec RejectionReason non null.
/// </summary>
public sealed record StorefrontStatsDto
{
    /// <summary>Vitrines en attente de validation (PendingReview).</summary>
    public int Pending { get; init; }
    /// <summary>Vitrines publiées sur Rue FactuTrust (Published).</summary>
    public int Published { get; init; }
    /// <summary>Vitrines refusées (Draft + RejectionReason renseigné).</summary>
    public int Rejected { get; init; }
    /// <summary>Vitrines suspendues (Suspended).</summary>
    public int Suspended { get; init; }
}

/// <summary>Migration status for one tenant database.</summary>
public sealed record MigrationStatusResultDto
{
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = null!;
    public bool HasMigrationsApplied { get; init; }
    public SubscriptionPlan? SubscriptionPlan { get; init; }
    public string? SubscriptionPlanDisplay { get; init; }
    public SubscriptionStatus? SubscriptionStatus { get; init; }
    public string? SubscriptionStatusDisplay { get; init; }
    public bool IsPayingSubscriber { get; init; }
}
