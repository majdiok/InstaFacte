namespace FactuTrust.Application.Features.Treasury.Dtos;

/// <summary>
/// Réponse unique de l'écran « Trésorerie prévisionnelle par IA ».
/// </summary>
/// <remarks>
/// Volontairement monolithique : l'écran affiche KPI, graphique, jauge, scénarios, facteurs,
/// alertes et tableau mensuel d'un seul tenant. Les découper en autant d'appels rendrait le
/// chargement bavard et laisserait des zones se contredire pendant le rafraîchissement.
/// </remarks>
public sealed record CashFlowForecastDto
{
    public Guid RunId { get; init; }
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public int HorizonMonths { get; init; }
    public string Currency { get; init; } = "TND";

    /// <summary>Solde de trésorerie réel au premier jour projeté.</summary>
    public decimal OpeningBalance { get; init; }

    /// <summary>Solde projeté en fin d'horizon. Peut être négatif.</summary>
    public decimal ClosingBalance { get; init; }

    public decimal TotalInflows { get; init; }
    public decimal TotalOutflows { get; init; }
    public decimal NetFlow { get; init; }

    /// <summary>Indice de confiance dans [0..100].</summary>
    public decimal ConfidencePercent { get; init; }

    public DateTime ComputedAt { get; init; }
    public int DurationMs { get; init; }

    /// <summary>Vrai quand les probabilités de scénario ont été ajustées par le modèle.</summary>
    public bool AiAdjustmentApplied { get; init; }

    public string? AiModelRef { get; init; }

    public IReadOnlyList<CashFlowBucketDto> Buckets { get; init; } = Array.Empty<CashFlowBucketDto>();
    public IReadOnlyList<CashFlowScenarioDto> Scenarios { get; init; } = Array.Empty<CashFlowScenarioDto>();
    public IReadOnlyList<CashFlowInsightDto> Insights { get; init; } = Array.Empty<CashFlowInsightDto>();

    /// <summary>Prochains encaissements attendus (bloc « Encaissements à venir » de l'écran).</summary>
    public IReadOnlyList<CashFlowLineDto> UpcomingInflows { get; init; } = Array.Empty<CashFlowLineDto>();

    /// <summary>Seuils de la jauge de position de trésorerie.</summary>
    public CashFlowThresholdsDto Thresholds { get; init; } = new();
}

public sealed record CashFlowBucketDto
{
    public int SequenceIndex { get; init; }
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public decimal OpeningBalance { get; init; }
    public decimal Inflows { get; init; }
    public decimal Outflows { get; init; }
    public decimal NetFlow { get; init; }
    public decimal ClosingBalance { get; init; }
    public decimal LowClosingBalance { get; init; }
    public decimal HighClosingBalance { get; init; }
}

public sealed record CashFlowScenarioDto
{
    /// <summary>optimistic | realistic | pessimistic</summary>
    public string Kind { get; init; } = null!;

    public decimal ClosingBalance { get; init; }
    public decimal NetFlow { get; init; }

    /// <summary>Probabilité affichée, éventuellement ajustée par le modèle.</summary>
    public decimal ProbabilityPercent { get; init; }

    /// <summary>
    /// Probabilité produite par le moteur seul. Toujours renvoyée : l'écran l'affiche en infobulle
    /// pour qu'un comptable puisse constater ce que l'IA a déplacé.
    /// </summary>
    public decimal DeterministicProbabilityPercent { get; init; }

    /// <summary>deterministic | ai_adjusted</summary>
    public string ProbabilitySource { get; init; } = null!;

    public string? AiRationale { get; init; }
}

public sealed record CashFlowInsightDto
{
    /// <summary>alert | driver | recommendation</summary>
    public string Kind { get; init; } = null!;

    /// <summary>info | warning | critical</summary>
    public string Severity { get; init; } = null!;

    /// <summary>rule | ai</summary>
    public string Origin { get; init; } = null!;

    /// <summary>low | medium | high — renseigné pour les facteurs d'influence.</summary>
    public string? Impact { get; init; }

    /// <summary>inflow | outflow — sens de l'effet sur le solde.</summary>
    public string? ImpactDirection { get; init; }

    public string Title { get; init; } = null!;
    public string? Detail { get; init; }
    public DateTime? PeriodStart { get; init; }
    public decimal? EstimatedBalance { get; init; }
}

public sealed record CashFlowLineDto
{
    public Guid Id { get; init; }

    /// <summary>inflow | outflow</summary>
    public string Direction { get; init; } = null!;

    /// <summary>Source métier, en snake_case (client_invoice, fiscal_obligation…).</summary>
    public string SourceType { get; init; } = null!;

    public Guid? SourceId { get; init; }
    public string? SourceReference { get; init; }
    public string Label { get; init; } = null!;
    public string? ThirdPartyName { get; init; }
    public DateTime ContractualDate { get; init; }
    public DateTime ExpectedDate { get; init; }
    public decimal Amount { get; init; }
    public decimal ProbabilityPercent { get; init; }
    public decimal WeightedAmount { get; init; }
    public bool IsConfirmed { get; init; }
}

public sealed record CashFlowThresholdsDto
{
    public decimal CriticalThreshold { get; init; }
    public decimal AlertThreshold { get; init; }
    public decimal ComfortThreshold { get; init; }
    public int? PayrollPaymentDayOfMonth { get; init; }
}

public sealed record RecurringCashCommitmentDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = null!;

    /// <summary>inflow | outflow</summary>
    public string Direction { get; init; } = null!;

    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TND";

    /// <summary>weekly | monthly | quarterly | semi_annual | annual</summary>
    public string Frequency { get; init; } = null!;

    public int DayOfMonth { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? Category { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>Création ou mise à jour d'un engagement récurrent.</summary>
public sealed record SaveRecurringCashCommitmentRequest
{
    public string Label { get; init; } = null!;
    public string Direction { get; init; } = "outflow";
    public decimal Amount { get; init; }
    public string Frequency { get; init; } = "monthly";
    public int DayOfMonth { get; init; } = 1;
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? Category { get; init; }
    public string? Notes { get; init; }
}

public sealed record SaveCashFlowThresholdsRequest
{
    public decimal CriticalThreshold { get; init; }
    public decimal AlertThreshold { get; init; }
    public decimal ComfortThreshold { get; init; }
    public int? PayrollPaymentDayOfMonth { get; init; }
}
