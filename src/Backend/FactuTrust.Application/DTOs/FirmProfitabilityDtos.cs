namespace FactuTrust.Application.DTOs;

public sealed record FirmDossierTimeProfitabilityRowDto
{
    public Guid? FirmClientAssignmentId { get; init; }
    public string CompanyName { get; init; } = null!;
    public int Year { get; init; }
    public Guid CollaboratorUserId { get; init; }
    public string CollaboratorName { get; init; } = null!;
    public decimal BudgetAnnuel { get; init; }
    public decimal TotalHours { get; init; }
    public decimal HourlyRate { get; init; }
    public decimal Cost { get; init; }
    public decimal Margin { get; init; }
    public bool BudgetFromFallback { get; init; }

    /// <summary>Heures imputables au client.</summary>
    public decimal BillableHours { get; init; }

    /// <summary>Heures passées sur le dossier sans être refacturables.</summary>
    public decimal NonBillableHours { get; init; }

    /// <summary>Part facturable du temps passé, en %. Révèle la charge improductive absorbée par le dossier.</summary>
    public decimal BillableRatioPercent { get; init; }

    /// <summary>Origine du taux horaire : 1 dérivé, 2 imposé, 3 profil, 4 défaut cabinet.</summary>
    public int HourlyRateSource { get; init; }

    public string HourlyRateSourceDisplay { get; init; } = null!;

    /// <summary>Détail du calcul du taux, affiché en regard du montant pour le rendre vérifiable.</summary>
    public string HourlyRateBasis { get; init; } = null!;
}

public sealed record FirmChartSliceDto
{
    public string Label { get; init; } = null!;
    public decimal Value { get; init; }
}

public sealed record FirmDossierTimeProfitabilityReportDto
{
    public IReadOnlyList<FirmDossierTimeProfitabilityRowDto> Rows { get; init; } = Array.Empty<FirmDossierTimeProfitabilityRowDto>();
    public decimal TotalHours { get; init; }
    public decimal TotalBillableHours { get; init; }
    public decimal TotalNonBillableHours { get; init; }
    public decimal UniqueBudgetSum { get; init; }
    public IReadOnlyList<FirmChartSliceDto> HoursByYear { get; init; } = Array.Empty<FirmChartSliceDto>();
    public IReadOnlyList<FirmChartSliceDto> HoursByCompany { get; init; } = Array.Empty<FirmChartSliceDto>();
}

public enum FirmMarginSignFilter
{
    All = 0,
    Negative = 1,
    Positive = 2
}

public sealed record UpsertFirmDossierYearBudgetDto
{
    public decimal BudgetAnnuel { get; init; }
}

public sealed record FirmDossierYearBudgetDto
{
    public Guid FirmClientAssignmentId { get; init; }
    public int Year { get; init; }
    public decimal BudgetAnnuel { get; init; }
    public bool FromFallback { get; init; }
}

public sealed record FirmHourlyRateSettingsDto
{
    public decimal DefaultHourlyCostRate { get; init; }
}

/// <summary>Coût employeur annuel d'un collaborateur et taux horaire qui en découle.</summary>
public sealed record FirmCollaboratorYearCostDto
{
    public Guid CollaboratorUserId { get; init; }
    public string CollaboratorName { get; init; } = null!;
    public int Year { get; init; }

    public decimal GrossAnnualSalary { get; init; }
    public decimal EmployerContributions { get; init; }
    public decimal PayrollExtras { get; init; }
    public decimal TotalEmployerCost { get; init; }

    /// <summary>0 = aucune donnée, 1 = saisi, 2 = importé de la paie du cabinet.</summary>
    public int Source { get; init; }
    public string SourceDisplay { get; init; } = null!;
    public DateTime? ImportedAt { get; init; }

    /// <summary>Pourquoi la ligne est dans cet état — voir <c>FirmCollaboratorCostDiagnostic</c>.</summary>
    public int CostDiagnostic { get; init; }

    /// <summary>Formulation lisible du diagnostic, affichée en regard de la ligne.</summary>
    public string CostDiagnosticDisplay { get; init; } = null!;

    /// <summary>Action à mener pour sortir de cet état, vide si la ligne est nominale.</summary>
    public string? CostDiagnosticHint { get; init; }

    public decimal? HourlyRateOverride { get; init; }
    public string? OverrideJustification { get; init; }

    /// <summary>Taux effectivement retenu, après application de la chaîne de repli.</summary>
    public decimal EffectiveHourlyRate { get; init; }
    public int HourlyRateSource { get; init; }
    public string HourlyRateSourceDisplay { get; init; } = null!;
    public string HourlyRateBasis { get; init; } = null!;

    /// <summary>Dénominateur du taux dérivé, affiché pour rendre le calcul vérifiable.</summary>
    public decimal AnnualProductiveHours { get; init; }

    /// <summary>0 = forfait d'exercice, 1 = individualisé sur les congés réels.</summary>
    public int ProductiveHoursMode { get; init; }

    /// <summary>Formulation lisible du dénominateur retenu.</summary>
    public string ProductiveHoursBasis { get; init; } = string.Empty;

    /// <summary>Congés approuvés de l'exercice décomptés du temps de présence.</summary>
    public decimal RealAbsenceDays { get; init; }

    /// <summary>Jours de congés retenus par les paramètres d'exercice, pour comparaison.</summary>
    public decimal ParametricLeaveDays { get; init; }

    /// <summary>Salarié de la paie du cabinet auquel le collaborateur est lié, le cas échéant.</summary>
    public Guid? PayrollEmployeeId { get; init; }

    public string? PayrollEmployeeName { get; init; }

    /// <summary>0 = aucune, 1 = manuelle, 2 = auto email.</summary>
    public int PayrollLinkSource { get; init; }

    public string PayrollLinkSourceDisplay { get; init; } = null!;

    public DateTime? PayrollLinkedAt { get; init; }

    public int PayslipCount { get; init; }
}

public sealed record SaveFirmCollaboratorYearCostDto
{
    public decimal GrossAnnualSalary { get; init; }

    /// <summary>
    /// Charges patronales. Laisser à null pour les faire calculer à partir des taux de l'exercice
    /// (CNSS patronale, TFP, FOPROLOS, accident du travail).
    /// </summary>
    public decimal? EmployerContributions { get; init; }

    public decimal PayrollExtras { get; init; }

    /// <summary>Taux horaire imposé. Null pour laisser le taux se dériver du coût employeur.</summary>
    public decimal? HourlyRateOverride { get; init; }

    /// <summary>Obligatoire dès qu'un taux est imposé.</summary>
    public string? OverrideJustification { get; init; }
}

public sealed record LinkFirmPayrollEmployeeDto
{
    /// <summary>Null pour rompre la liaison.</summary>
    public Guid? PayrollEmployeeId { get; init; }
}

/// <summary>Une liaison collaborateur ↔ salarié de paie à établir.</summary>
public sealed record FirmPayrollEmployeeLinkRequest(Guid CollaboratorUserId, Guid PayrollEmployeeId);

/// <summary>Issue d'une pose de liaisons en lot.</summary>
public sealed record FirmPayrollLinkBatchResultDto
{
    public int Linked { get; init; }

    /// <summary>Liaisons écartées, avec leur motif — aucune n'interrompt les autres.</summary>
    public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
}

public sealed record FirmPayrollImportResultDto
{
    public int Imported { get; init; }

    /// <summary>Collaborateurs sans liaison paie, donc non importables en l'état.</summary>
    public int Unlinked { get; init; }

    /// <summary>Lignes laissées intactes car saisies manuellement (import non forcé).</summary>
    public int SkippedManual { get; init; }

    /// <summary>Imports déjà à jour par rapport à la dernière paie validée.</summary>
    public int SkippedUpToDate { get; init; }

    public bool PayrollAvailable { get; init; }
    public string? UnavailableReason { get; init; }
}

/// <summary>Déclencheur d'une synchronisation des coûts collaborateurs.</summary>
public enum FirmCostSyncTrigger
{
    ManualImport = 1,
    ManualSync = 2,
    PayrollValidate = 3,
    RentabilityPrefill = 4
}

public sealed record FirmCollaboratorCostSyncResultDto
{
    public bool PayrollAvailable { get; init; }
    public string? UnavailableReason { get; init; }
    public int LinkedByEmail { get; init; }
    public int Imported { get; init; }
    public int SkippedManual { get; init; }
    public int SkippedUnlinked { get; init; }
    public int SkippedUpToDate { get; init; }
    public DateTime SyncedAt { get; init; }
}

public sealed record FirmCollaboratorRentabilityListItemDto
{
    public Guid Id { get; init; }
    public Guid CollaboratorUserId { get; init; }
    public string CollaboratorName { get; init; } = null!;
    public int Year { get; init; }
    public int AttachedCollaboratorsCount { get; init; }
    public int CompaniesCount { get; init; }
    public decimal TotalRevenue { get; init; }
    public decimal PayrollCost { get; init; }
    public decimal AdminPayrollCharge { get; init; }
    public decimal ItManagementCharge { get; init; }
    public decimal OperatingCharge { get; init; }
    public decimal ClientDebitBalance { get; init; }
    public decimal ClientCreditBalance { get; init; }
    public decimal Rentability { get; init; }

    /// <summary>Rentabilité diminuée des honoraires non recouvrés. Indicateur dérivé, jamais stocké.</summary>
    public decimal CollectedRentability { get; init; }

    /// <summary>Part du chiffre d'affaires effectivement encaissée, en %.</summary>
    public decimal RecoveryRatePercent { get; init; }
}

public sealed record FirmCollaboratorRentabilityListDto
{
    public IReadOnlyList<FirmCollaboratorRentabilityListItemDto> Items { get; init; } = Array.Empty<FirmCollaboratorRentabilityListItemDto>();
    public FirmCollaboratorRentabilityListItemDto Totals { get; init; } = null!;

    /// <summary>
    /// Alertes de cohérence sur les totaux — notamment le double comptage des charges lorsqu'un
    /// manager et ses rattachés disposent chacun d'un snapshot sur le même exercice.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed record FirmRentabilityPortfolioRowDto
{
    public Guid FirmClientAssignmentId { get; init; }
    public string CompanyName { get; init; } = null!;
    public string CollaboratorName { get; init; } = null!;
    public decimal AnnualFeeHt { get; init; }
    public decimal ClientDebitBalance { get; init; }
    public decimal ClientCreditBalance { get; init; }

    /// <summary>Heures passées sur ce dossier par le collaborateur et ses rattachés.</summary>
    public decimal PortfolioHours { get; init; }

    /// <summary>Heures passées sur ce dossier par l'ensemble du cabinet — dénominateur de la quote-part.</summary>
    public decimal TotalDossierHours { get; init; }

    /// <summary>
    /// Quote-part d'honoraires revenant au portefeuille : honoraires × heures du portefeuille ÷ heures totales.
    /// </summary>
    public decimal RevenueShare { get; init; }
}

public sealed record FirmRentabilityPayrollRowDto
{
    public Guid CollaboratorUserId { get; init; }
    public string CollaboratorName { get; init; } = null!;
    public decimal GrossSalary { get; init; }
    public decimal EmployerContributions { get; init; }
    public decimal PayrollExtras { get; init; }
    public decimal AnnualTotal => GrossSalary + EmployerContributions + PayrollExtras;
}

public sealed record FirmCollaboratorRentabilityDetailDto
{
    public Guid? Id { get; init; }
    public Guid CollaboratorUserId { get; init; }
    public string CollaboratorName { get; init; } = null!;
    public int Year { get; init; }
    public int AttachedCollaboratorsCount { get; init; }
    public decimal TotalRevenue { get; init; }
    public decimal CalculatedTotalRevenue { get; init; }
    public decimal PayrollCost { get; init; }
    public decimal CalculatedPayrollCost { get; init; }
    public decimal AdminPayrollCharge { get; init; }
    public decimal ItManagementCharge { get; init; }
    public decimal OperatingCharge { get; init; }
    public decimal ClientDebitBalance { get; init; }
    public decimal ClientCreditBalance { get; init; }
    public decimal Rentability { get; init; }

    /// <summary>Quote-part de structure calculée, à afficher en lecture seule en regard du champ.</summary>
    public decimal CalculatedSupportShare { get; init; }

    public IReadOnlyList<FirmRentabilityPortfolioRowDto> Portfolio { get; init; } = Array.Empty<FirmRentabilityPortfolioRowDto>();
    public IReadOnlyList<FirmRentabilityPayrollRowDto> PayrollRows { get; init; } = Array.Empty<FirmRentabilityPayrollRowDto>();
}

public sealed record SaveFirmCollaboratorRentabilityDto
{
    public Guid CollaboratorUserId { get; init; }
    public int Year { get; init; }
    public decimal TotalRevenue { get; init; }
    public decimal PayrollCost { get; init; }
    public decimal AdminPayrollCharge { get; init; }
    public decimal ItManagementCharge { get; init; }
    public decimal OperatingCharge { get; init; }
    public decimal ClientDebitBalance { get; init; }
    public decimal ClientCreditBalance { get; init; }
    public int CompaniesCount { get; init; }
    public int AttachedCollaboratorsCount { get; init; }
    public IReadOnlyList<FirmRentabilityPayrollRowDto>? PayrollRows { get; init; }
}

public sealed record DuplicateFirmRentabilityResultDto
{
    public int Duplicated { get; init; }
    public int Skipped { get; init; }
}

/// <summary>Compte rendu d'un recalcul global des marges collaborateur.</summary>
public sealed record FirmRentabilityRecalculationResultDto
{
    public int Recalculated { get; init; }

    /// <summary>
    /// Snapshots laissés intacts, avec leur motif — typiquement un exercice dépourvu de feuilles
    /// de temps, qu'un recalcul ramènerait à zéro.
    /// </summary>
    public IReadOnlyList<FirmRentabilitySkippedSnapshotDto> Skipped { get; init; } =
        Array.Empty<FirmRentabilitySkippedSnapshotDto>();
}

public sealed record FirmRentabilitySkippedSnapshotDto
{
    public Guid Id { get; init; }
    public string CollaboratorName { get; init; } = null!;
    public int Year { get; init; }
    public string Reason { get; init; } = null!;
}
