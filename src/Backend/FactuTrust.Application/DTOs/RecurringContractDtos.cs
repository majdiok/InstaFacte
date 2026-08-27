using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

public sealed record RecurringContractListQuery
{
    public string? Search { get; init; }
    public RecurringContractStatus? Status { get; init; }
    public Guid? ClientId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record RecurringContractListItemDto
{
    public Guid Id { get; init; }
    public string? Number { get; init; }
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = null!;
    public RecurringContractStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public BillingFrequency BillingFrequency { get; init; }
    public string BillingFrequencyDisplay { get; init; } = null!;
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime? NextBillingDate { get; init; }
    public string Currency { get; init; } = "TND";
    public decimal EstimatedMonthlyAmount { get; init; }
}

public sealed record RecurringContractDto
{
    public Guid Id { get; init; }
    public string? Number { get; init; }
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = null!;
    public RecurringContractStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public BillingFrequency BillingFrequency { get; init; }
    public string BillingFrequencyDisplay { get; init; } = null!;
    public int BillingDayOfMonth { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime? NextBillingDate { get; init; }
    public DateTime? LastBilledPeriodEnd { get; init; }
    public Guid? PaymentTermTemplateId { get; init; }
    public Guid? PriceListId { get; init; }
    public bool AutoRenew { get; init; }
    public int NoticePeriodDays { get; init; }
    public string Currency { get; init; } = "TND";
    public Guid? SourceQuoteId { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public bool SetupFeeBilled { get; init; }
    public IReadOnlyList<RecurringContractLineDto> Lines { get; init; } = Array.Empty<RecurringContractLineDto>();
}

public sealed record RecurringContractLineDto
{
    public Guid Id { get; init; }
    public RecurringContractLineType LineType { get; init; }
    public string LineTypeDisplay { get; init; } = null!;
    public Guid? ProductId { get; init; }
    public string Description { get; init; } = null!;
    public decimal Quantity { get; init; }
    public decimal UnitPriceHT { get; init; }
    public decimal VatRate { get; init; }
    public Guid? UsageMetricId { get; init; }
    public string? UsageMetricName { get; init; }
    public decimal? IncludedQuantity { get; init; }
    public decimal? OverageUnitPriceHT { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
}

public sealed record UpsertRecurringContractDto
{
    public Guid ClientId { get; init; }
    public BillingFrequency BillingFrequency { get; init; }
    public int BillingDayOfMonth { get; init; } = 1;
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public bool AutoRenew { get; init; } = true;
    public int NoticePeriodDays { get; init; } = 30;
    public Guid? PaymentTermTemplateId { get; init; }
    public Guid? PriceListId { get; init; }
    public Guid? SourceQuoteId { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<UpsertRecurringContractLineDto> Lines { get; init; } = Array.Empty<UpsertRecurringContractLineDto>();
}

public sealed record UpsertRecurringContractLineDto
{
    public Guid? Id { get; init; }
    public RecurringContractLineType LineType { get; init; }
    public Guid? ProductId { get; init; }
    public string Description { get; init; } = null!;
    public decimal Quantity { get; init; }
    public decimal UnitPriceHT { get; init; }
    public decimal VatRate { get; init; } = 19m;
    public Guid? UsageMetricId { get; init; }
    public decimal? IncludedQuantity { get; init; }
    public decimal? OverageUnitPriceHT { get; init; }
    public int SortOrder { get; init; }
}

public sealed record UsageMetricDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Unit { get; init; } = null!;
    public UsageAggregationMode AggregationMode { get; init; }
    public string AggregationModeDisplay { get; init; } = null!;
    public Guid? ProductId { get; init; }
    public bool IsActive { get; init; }
}

public sealed record UpsertUsageMetricDto
{
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Unit { get; init; } = null!;
    public UsageAggregationMode AggregationMode { get; init; }
    public Guid? ProductId { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record UsageRecordDto
{
    public Guid Id { get; init; }
    public Guid RecurringContractId { get; init; }
    public Guid UsageMetricId { get; init; }
    public string UsageMetricName { get; init; } = null!;
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    public decimal Quantity { get; init; }
    public UsageRecordSource Source { get; init; }
    public string SourceDisplay { get; init; } = null!;
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record RecordUsageDto
{
    public Guid UsageMetricId { get; init; }
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    public decimal Quantity { get; init; }
    public string? Notes { get; init; }
}

public sealed record RecurringContractBillingRunDto
{
    public Guid Id { get; init; }
    public Guid RecurringContractId { get; init; }
    public string? ContractNumber { get; init; }
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    public RecurringContractBillingRunStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public Guid? InvoiceDraftId { get; init; }
    public Guid? InvoiceId { get; init; }
    public decimal FixedAmount { get; init; }
    public decimal UsageAmount { get; init; }
    public decimal ProrationAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record PendingRecurringDraftDto
{
    public Guid BillingRunId { get; init; }
    public Guid RecurringContractId { get; init; }
    public string? ContractNumber { get; init; }
    public string ClientName { get; init; } = null!;
    public Guid InvoiceDraftId { get; init; }
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    public decimal TotalAmount { get; init; }
}

public sealed record AmendRecurringContractDto
{
    public RecurringContractAmendmentType AmendmentType { get; init; }
    public DateTime EffectiveDate { get; init; }
    public ProrationPolicy ProrationPolicy { get; init; } = ProrationPolicy.DailyProration;
    public string? Notes { get; init; }
    public UpsertRecurringContractDto? UpdatedContract { get; init; }
}

public sealed record ImportUsageRecordRowDto
{
    public string MetricCode { get; init; } = null!;
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    public decimal Quantity { get; init; }
    public string? Notes { get; init; }
}

// ────────────────────────────────────────────────────────────────────────────
// Extensions v1 (2026-08) — ajouts strictement additifs, aucun DTO existant modifié.
// ────────────────────────────────────────────────────────────────────────────

public sealed record CloneRecurringContractDto
{
    /// <summary>Nouvelle date de début (défaut : aujourd'hui UTC).</summary>
    public DateTime? StartDate { get; init; }
    /// <summary>Client de substitution (défaut : client d'origine).</summary>
    public Guid? ClientId { get; init; }
    /// <summary>Référence de substitution (défaut : référence d'origine recopiée).</summary>
    public string? Reference { get; init; }
}

public sealed record RenewRecurringContractDto
{
    /// <summary>Note libre tracée sur l'avenant de renouvellement.</summary>
    public string? Notes { get; init; }
}

public sealed record RecurringContractAmendmentDto
{
    public Guid Id { get; init; }
    public RecurringContractAmendmentType Type { get; init; }
    public string TypeDisplay { get; init; } = null!;
    public DateTime EffectiveDate { get; init; }
    public ProrationPolicy ProrationPolicy { get; init; }
    public string ProrationPolicyDisplay { get; init; } = null!;
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public string? CreatedByUserName { get; init; }   // null si utilisateur introuvable/inactif
}

public sealed record RecurringContractAmendmentDetailDto
{
    public Guid Id { get; init; }
    public Guid RecurringContractId { get; init; }
    public RecurringContractAmendmentType Type { get; init; }
    public string TypeDisplay { get; init; } = null!;
    public DateTime EffectiveDate { get; init; }
    public ProrationPolicy ProrationPolicy { get; init; }
    public string ProrationPolicyDisplay { get; init; } = null!;
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public string? CreatedByUserName { get; init; }
    /// <summary>Snapshot JSON des lignes (ou du header pour Renewal) avant l'avenant.</summary>
    public string? SnapshotBeforeJson { get; init; }
    /// <summary>Snapshot JSON après l'avenant.</summary>
    public string? SnapshotAfterJson { get; init; }
}

public sealed record RecurringContractScheduleItemDto
{
    /// <summary>Date d'échéance (jour de facturation).</summary>
    public DateTime Date { get; init; }
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    /// <summary>Ex. « Période du 01/09/2026 au 30/09/2026 ».</summary>
    public string Description { get; init; } = null!;
    /// <summary>HT estimé : lignes fixes proratisées + estimation usage (moyenne 3 derniers runs Invoiced).
    /// Pour les runs passés non fusionnés : montant réel du run.</summary>
    public decimal EstimatedAmountHT { get; init; }
    /// <summary>TTC estimé (0 pour les runs passés sans fusion — colonne « estimée » par nature).</summary>
    public decimal EstimatedAmountTTC { get; init; }
    public RecurringContractScheduleOccurrenceStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    /// <summary>Run associé quand l'occurrence a déjà été matérialisée (null sinon).</summary>
    public Guid? BillingRunId { get; init; }
    /// <summary>Dénormalisé du run fusionné : permet au frontend les liens directs wizard/facture sans appel supplémentaire.</summary>
    public Guid? InvoiceDraftId { get; init; }
    public Guid? InvoiceId { get; init; }
}

public sealed record RecurringContractFinancialSummaryDto
{
    public Guid ContractId { get; init; }
    /// <summary>Début de la fenêtre contractuelle retenue (traçabilité de la règle).</summary>
    public DateTime WindowFrom { get; init; }
    /// <summary>Fin de la fenêtre : EndDate si définie, sinon 12 mois glissants.</summary>
    public DateTime WindowTo { get; init; }
    /// <summary>true si le contrat n'a pas de EndDate (total calculé sur 12 mois glissants).</summary>
    public bool IsOpenEnded { get; init; }
    /// <summary>HT estimé des échéances de la fenêtre (même calcul que /schedule).</summary>
    public decimal TotalContractAmount { get; init; }
    /// <summary>HT réellement facturé net : factures non annulées (SubTotal réel), avoirs déduits, runs DraftCreated exclus.</summary>
    public decimal TotalInvoicedAmount { get; init; }
    /// <summary>TotalContractAmount − TotalInvoicedAmount (peut être négatif en cas de surfacturation).</summary>
    public decimal RemainingAmount { get; init; }
    /// <summary>0 si TotalContractAmount = 0 ; arrondi 2 décimales ; peut dépasser 100.</summary>
    public decimal PercentInvoiced { get; init; }
    /// <summary>Runs Invoiced dont la facture n'est pas annulée.</summary>
    public int InvoicedRunsCount { get; init; }
    /// <summary>Nombre d'occurrences de la fenêtre contractuelle.</summary>
    public int TotalRunsCount { get; init; }
    public string Currency { get; init; } = "TND";
}

public sealed record RecurringContractLinkedInvoiceDto
{
    public Guid InvoiceId { get; init; }
    public string Number { get; init; } = null!;
    /// <summary>Date d'émission (IssueDate).</summary>
    public DateTime Date { get; init; }
    /// <summary>Date d'échéance de paiement (DueDate de l'entité Invoice) — colonne « Échéance » de l'onglet Factures.</summary>
    public DateTime? DueDate { get; init; }
    /// <summary>SubTotal HT (négatif pour un avoir).</summary>
    public decimal AmountHT { get; init; }
    /// <summary>TotalAmount TTC (négatif pour un avoir).</summary>
    public decimal AmountTTC { get; init; }
    public InvoiceStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public bool IsCreditNote { get; init; }
    /// <summary>Run à l'origine de la facture (ou du document d'origine pour un avoir) ; null si absent.</summary>
    public Guid? BillingRunId { get; init; }
    public DateTime? PeriodFrom { get; init; }
    public DateTime? PeriodTo { get; init; }
}

public sealed record RecurringContractEvolutionPointDto
{
    /// <summary>Mois au format « yyyy-MM » (mois de PeriodTo du run).</summary>
    public string Month { get; init; } = null!;
    /// <summary>Montant HT net facturé sur le mois (même règle que /financial-summary ; 0 si aucun run).</summary>
    public decimal Amount { get; init; }
}

/// <summary>
/// Vue détail enrichie (GET /{id}/detail). Recopie tous les champs de RecurringContractDto
/// (qui reste inchangé pour GET /{id}) et ajoute les indicateurs calculés.
/// </summary>
public sealed record RecurringContractDetailDto
{
    // — champs recopiés à l'identique de RecurringContractDto —
    public Guid Id { get; init; }
    public string? Number { get; init; }
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = null!;
    public RecurringContractStatus Status { get; init; }
    public string StatusDisplay { get; init; } = null!;
    public BillingFrequency BillingFrequency { get; init; }
    public string BillingFrequencyDisplay { get; init; } = null!;
    public int BillingDayOfMonth { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime? NextBillingDate { get; init; }
    public DateTime? LastBilledPeriodEnd { get; init; }
    public Guid? PaymentTermTemplateId { get; init; }
    public Guid? PriceListId { get; init; }
    public bool AutoRenew { get; init; }
    public int NoticePeriodDays { get; init; }
    public string Currency { get; init; } = "TND";
    public Guid? SourceQuoteId { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
    public bool SetupFeeBilled { get; init; }
    public IReadOnlyList<RecurringContractLineDto> Lines { get; init; } = Array.Empty<RecurringContractLineDto>();

    // — champs enrichis —
    /// <summary>Estimation mensuelle normalisée (÷3 trimestriel, ÷12 annuel), lignes fixes actives.</summary>
    public decimal EstimatedMonthlyAmount { get; init; }
    /// <summary>Occurrences À venir dans la fenêtre contractuelle (EndDate, sinon 12 mois glissants).</summary>
    public int UpcomingOccurrencesCount { get; init; }
    /// <summary>EndDate − NoticePeriodDays ; null si le contrat n'a pas de EndDate.</summary>
    public DateTime? CancellationDeadline { get; init; }
    /// <summary>Total HT « plein tarif » des lignes FixedRecurring actives aujourd'hui + OneTimeSetup non facturé (hors usage, hors prorata).</summary>
    public decimal CurrentPeriodTotalHT { get; init; }
    public decimal CurrentPeriodTotalTVA { get; init; }
    public decimal CurrentPeriodTotalTTC { get; init; }
}

public sealed record UpdateRecurringContractNotesDto
{
    public string? Notes { get; init; }
}

public sealed record RecurringContractStatsDto
{
    /// <summary>Contrats au statut Active.</summary>
    public int ActiveCount { get; init; }
    /// <summary>Somme des EstimatedMonthlyAmount (même normalisation que ListAsync) sur les contrats Active.</summary>
    public decimal EstimatedMonthlyRecurringTotal { get; init; }
    /// <summary>Contrats Active dont NextBillingDate ≤ aujourd'hui + 7 jours.</summary>
    public int DueSoonCount { get; init; }
    /// <summary>Runs DraftCreated avec brouillon associé (tous contrats).</summary>
    public int PendingDraftsCount { get; init; }
    public string Currency { get; init; } = "TND";
}
