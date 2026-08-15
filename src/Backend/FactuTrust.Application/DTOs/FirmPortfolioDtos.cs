using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

/// <summary>
/// Santé du fan-out multi-dossiers. Contrairement à <c>FirmFiscalOpsSummaryDto</c>, dont les
/// compteurs à zéro ne distinguent pas « tout est sain » de « toutes les bases sont injoignables »,
/// l'agent doit pouvoir dire à l'utilisateur qu'une partie du portefeuille n'a pas pu être lue.
/// </summary>
public sealed record FirmFanOutHealthDto
{
    public int DossiersRead { get; init; }
    public int DossiersFailed { get; init; }
    public bool IsPartial => DossiersFailed > 0;
}

/// <summary>Vue consolidée du portefeuille du cabinet (agent Chef de mission).</summary>
public sealed record FirmPortfolioOverviewDto
{
    public int ActiveDossiersCount { get; init; }
    public int OverdueCount { get; init; }
    public int UpcomingWithin7DaysCount { get; init; }
    public int UpcomingAfter7DaysCount { get; init; }
    public decimal OverdueEstimatedAmount { get; init; }
    public decimal UpcomingWithin7DaysEstimatedAmount { get; init; }
    /// <summary>Nombre de dossiers portant au moins une échéance en retard.</summary>
    public int DossiersWithOverdueCount { get; init; }
    public int InactiveDossiers30DaysCount { get; init; }
    public int VatDraftsCount { get; init; }
    public string Currency { get; init; } = "TND";
    public DateTime GeneratedAt { get; init; }
    public FirmFanOutHealthDto FanOut { get; init; } = new();
}

/// <summary>Une échéance fiscale, identifiée par son dossier (le cabinet en suit plusieurs).</summary>
public sealed record FirmDeadlineRowDto
{
    public Guid Id { get; init; }
    public Guid CompanyTenantId { get; init; }
    public string CompanyName { get; init; } = null!;
    public string ObligationLabel { get; init; } = null!;
    public FiscalObligationType ObligationType { get; init; }
    public DateTime DueDate { get; init; }
    /// <summary>Négatif = en retard de N jours. Épargne au modèle un calcul de dates.</summary>
    public int DaysUntilDue { get; init; }
    public FiscalScheduleStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public decimal EstimatedAmount { get; init; }
    public Guid? ResponsibleUserId { get; init; }
    public string? ResponsibleName { get; init; }
    public DateTime? LastReminderAt { get; init; }
}

public sealed record FirmDeadlineListDto
{
    public IReadOnlyList<FirmDeadlineRowDto> Items { get; init; } = Array.Empty<FirmDeadlineRowDto>();
    /// <summary>Total correspondant aux filtres AVANT bornage au top N (le modèle doit savoir qu'il en reste).</summary>
    public int TotalMatching { get; init; }
    public string Currency { get; init; } = "TND";
    public DateTime GeneratedAt { get; init; }
    public FirmFanOutHealthDto FanOut { get; init; } = new();
}

/// <summary>Santé d'un dossier, classée par risque décroissant.</summary>
public sealed record FirmDossierHealthRowDto
{
    public Guid CompanyTenantId { get; init; }
    public string CompanyName { get; init; } = null!;
    /// <summary>Score de risque composite (cf. <c>FirmPortfolioRiskScore</c>), décroissant.</summary>
    public int RiskScore { get; init; }
    public int OverdueCount { get; init; }
    public decimal OverdueEstimatedAmount { get; init; }
    public int UpcomingWithin7DaysCount { get; init; }
    public DateTime? LastJournalEntryDate { get; init; }
    public bool IsInactive30Days { get; init; }
    public int VatDraftsCount { get; init; }
    public string? AssignedAccountantName { get; init; }
    /// <summary>Vrai si la base du dossier n'a pas pu être lue : les compteurs ne font pas foi.</summary>
    public bool ReadFailed { get; init; }
}

public sealed record FirmDossierHealthListDto
{
    public IReadOnlyList<FirmDossierHealthRowDto> Items { get; init; } = Array.Empty<FirmDossierHealthRowDto>();
    public int TotalDossiers { get; init; }
    public string Currency { get; init; } = "TND";
    public DateTime GeneratedAt { get; init; }
    public FirmFanOutHealthDto FanOut { get; init; } = new();
}

/// <summary>Charge d'un collaborateur, mesurée par les échéances dont il est responsable.</summary>
public sealed record FirmCollaboratorWorkloadRowDto
{
    public Guid CollaboratorUserId { get; init; }
    public string CollaboratorName { get; init; } = null!;
    public int DossiersCount { get; init; }
    public int OverdueCount { get; init; }
    public int UpcomingWithin7DaysCount { get; init; }
    public decimal OverdueEstimatedAmount { get; init; }
}

public sealed record FirmCollaboratorWorkloadDto
{
    public IReadOnlyList<FirmCollaboratorWorkloadRowDto> Items { get; init; } = Array.Empty<FirmCollaboratorWorkloadRowDto>();
    /// <summary>Échéances actionnables sans responsable désigné : angle mort du cabinet.</summary>
    public int UnassignedDeadlinesCount { get; init; }
    public string Currency { get; init; } = "TND";
    public DateTime GeneratedAt { get; init; }
    public FirmFanOutHealthDto FanOut { get; init; } = new();
}

/// <summary>Filtres de <c>GetDeadlinesAsync</c>. Tous optionnels ; le bornage est toujours appliqué.</summary>
public sealed record FirmDeadlineQuery
{
    /// <summary>Ne garder que les échéances déjà en retard.</summary>
    public bool OnlyOverdue { get; init; }
    /// <summary>Fenêtre « à venir » en jours (bornée à [1, 365]). Null = horizon par défaut.</summary>
    public int? WithinDays { get; init; }
    public FiscalObligationType? ObligationType { get; init; }
    public Guid? CompanyTenantId { get; init; }
    public Guid? ResponsibleUserId { get; init; }
    /// <summary>Nombre de lignes renvoyées (borné à [1, 50]) : contrainte de fenêtre de contexte du modèle.</summary>
    public int TopN { get; init; } = 20;
}
