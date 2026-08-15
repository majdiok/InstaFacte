namespace FactuTrust.Application.DTOs;

/// <summary>
/// Vue de révision consolidée du portefeuille : combien d'anomalies, de quelle gravité, sur quels
/// dossiers, et pour quel impact chiffré.
/// </summary>
public sealed record FirmRevisionOverviewDto
{
    public int FiscalYear { get; init; }
    public int DossiersCount { get; init; }

    /// <summary>Dossiers portant au moins une anomalie ouverte.</summary>
    public int DossiersWithAnomaliesCount { get; init; }

    /// <summary>Dossiers dont le dernier contrôle ne remonte à rien : jamais balayés.</summary>
    public int DossiersNeverScannedCount { get; init; }

    public int BlockingCount { get; init; }
    public int WarningCount { get; init; }
    public int InfoCount { get; init; }
    public int TotalAnomalies { get; init; }

    /// <summary>
    /// Somme des montants des seules anomalies dont la règle produit un impact chiffré signifiant.
    /// Additionner les autres (écritures en brouillard, trous de numérotation…) donnerait un total
    /// faux.
    /// </summary>
    public decimal TotalImpactAmount { get; init; }

    public string Currency { get; init; } = "TND";
    public DateTime GeneratedAt { get; init; }

    public IReadOnlyList<FirmRevisionDossierRowDto> Dossiers { get; init; }
        = Array.Empty<FirmRevisionDossierRowDto>();

    public IReadOnlyList<FirmRevisionFamilySliceDto> ByFamily { get; init; }
        = Array.Empty<FirmRevisionFamilySliceDto>();

    public FirmFanOutHealthDto FanOut { get; init; } = new();
}

/// <summary>Un dossier du portefeuille, vu sous l'angle de la révision.</summary>
public sealed record FirmRevisionDossierRowDto
{
    public Guid CompanyTenantId { get; init; }
    public string CompanyName { get; init; } = null!;
    public string? AssignedAccountantName { get; init; }
    public Guid? AssignedAccountantUserId { get; init; }

    /// <summary>
    /// Indice de risque, calculé : <c>bloquants × 10 + avertissements × 3 + infos</c>, majoré si le
    /// dossier n'a jamais été balayé. Sert au tri de la file de travail — il n'est pas rédigé par
    /// un modèle et ne dépend d'aucun aléa.
    /// </summary>
    public int RiskScore { get; init; }

    public int BlockingCount { get; init; }
    public int WarningCount { get; init; }
    public int InfoCount { get; init; }
    public int TotalAnomalies { get; init; }
    public decimal ImpactAmount { get; init; }

    public decimal? ComplianceRate { get; init; }
    public DateTime? LastScanAt { get; init; }

    /// <summary>Vrai si la base du dossier n'a pas pu être lue : les compteurs sont alors nuls et non fiables.</summary>
    public bool ReadFailed { get; init; }

    /// <summary>Vrai si aucun contrôle n'a jamais tourné sur ce dossier pour l'exercice.</summary>
    public bool NeverScanned { get; init; }

    public bool HasRevisionNote { get; init; }
}

/// <summary>Répartition des anomalies par domaine de contrôle, à l'échelle du portefeuille.</summary>
public sealed record FirmRevisionFamilySliceDto
{
    public string ModuleCode { get; init; } = null!;
    public string Label { get; init; } = null!;
    public int Count { get; init; }
    public int BlockingCount { get; init; }
    public decimal ImpactAmount { get; init; }
}

/// <summary>Détail d'un dossier : ses anomalies ouvertes, et la note de révision si elle existe.</summary>
public sealed record FirmRevisionDossierDetailDto
{
    public Guid CompanyTenantId { get; init; }
    public string CompanyName { get; init; } = null!;
    public int FiscalYear { get; init; }
    public decimal? ComplianceRate { get; init; }
    public DateTime? LastScanAt { get; init; }
    public decimal ImpactAmount { get; init; }

    public IReadOnlyList<AccountingAnomalyListItemDto> Anomalies { get; init; }
        = Array.Empty<AccountingAnomalyListItemDto>();

    public FirmRevisionNoteDto? Note { get; init; }
}

/// <summary>Le dossier de révision rédigé d'un contrôle.</summary>
public sealed record FirmRevisionNoteDto
{
    public Guid Id { get; init; }
    public Guid RunId { get; init; }
    public int FiscalYear { get; init; }
    public DateTime GeneratedAt { get; init; }
    public string? GeneratedByUserName { get; init; }

    /// <summary>Faux = note purement déterministe (modèle indisponible ou réponse écartée).</summary>
    public bool AiGenerated { get; init; }

    public string? ModelRef { get; init; }
    public string? FallbackReason { get; init; }
    public string ExecutiveSummary { get; init; } = string.Empty;
    public decimal TotalImpactAmount { get; init; }
    public int AnomalyCount { get; init; }
    public int BlockingCount { get; init; }

    public IReadOnlyList<FirmRevisionNoteItemDto> Items { get; init; }
        = Array.Empty<FirmRevisionNoteItemDto>();
}

/// <summary>
/// Une entrée du dossier de révision : l'anomalie déterministe, augmentée de sa rédaction.
/// Sévérité, montant, compte et pièce viennent de l'anomalie — jamais du texte généré.
/// </summary>
public sealed record FirmRevisionNoteItemDto
{
    public Guid AnomalyId { get; init; }
    public string RuleCode { get; init; } = null!;
    public string ModuleCode { get; init; } = null!;
    public int Severity { get; init; }
    public string Title { get; init; } = null!;

    /// <summary>Impact chiffré, ou null quand la règle ne produit pas de montant signifiant.</summary>
    public decimal? ImpactAmount { get; init; }

    public string? AccountRef { get; init; }
    public string? PieceRef { get; init; }

    /// <summary>Rédaction en style note de travail. Repli sur la description déterministe si besoin.</summary>
    public string WorkingNote { get; init; } = string.Empty;

    public string? ClientQuestion { get; init; }

    /// <summary>Action retenue, prise dans un ensemble fermé (cf. <c>RevisionAction</c>).</summary>
    public string Action { get; init; } = null!;

    public string ActionLabel { get; init; } = null!;
}

/// <summary>File de travail priorisée, ventilée par collaborateur affecté.</summary>
public sealed record FirmRevisionWorkQueueDto
{
    public int FiscalYear { get; init; }
    public DateTime GeneratedAt { get; init; }

    public IReadOnlyList<FirmRevisionCollaboratorLoadDto> Collaborators { get; init; }
        = Array.Empty<FirmRevisionCollaboratorLoadDto>();

    /// <summary>Dossiers sans collaborateur affecté : personne ne les traitera spontanément.</summary>
    public IReadOnlyList<FirmRevisionDossierRowDto> Unassigned { get; init; }
        = Array.Empty<FirmRevisionDossierRowDto>();

    public FirmFanOutHealthDto FanOut { get; init; } = new();
}

public sealed record FirmRevisionCollaboratorLoadDto
{
    public Guid? UserId { get; init; }
    public string Name { get; init; } = null!;
    public int DossiersCount { get; init; }
    public int BlockingCount { get; init; }
    public int TotalAnomalies { get; init; }
    public decimal ImpactAmount { get; init; }

    /// <summary>Dossiers du collaborateur, du plus risqué au moins risqué.</summary>
    public IReadOnlyList<FirmRevisionDossierRowDto> Dossiers { get; init; }
        = Array.Empty<FirmRevisionDossierRowDto>();
}

/// <summary>
/// Données du dossier de révision imprimé. Assemblé par la couche service, consommé par le rendu
/// PDF : ce dernier ne calcule rien, il met en page des valeurs déjà arrêtées.
/// </summary>
public sealed record RevisionDossierPdfContext
{
    public AccountingReportHeader Header { get; init; } = null!;
    public FirmRevisionNoteDto Note { get; init; } = null!;

    /// <summary>Taux de conformité du contrôle d'origine, quand il est connu.</summary>
    public decimal? ComplianceRate { get; init; }

    /// <summary>Date du contrôle dont la note est issue — la note en découle, elle ne la remplace pas.</summary>
    public DateTime? ControlCompletedAt { get; init; }
}

/// <summary>Résultat d'un balayage de portefeuille.</summary>
public sealed record FirmRevisionSweepResultDto
{
    public int FiscalYear { get; init; }
    public int DossiersScanned { get; init; }
    public int DossiersFailed { get; init; }
    public int TotalAnomalies { get; init; }
    public TimeSpan Duration { get; init; }
    public DateTime CompletedAt { get; init; }
}
