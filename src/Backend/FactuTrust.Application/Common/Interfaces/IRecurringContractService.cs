using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces;

public interface IRecurringContractService
{
    Task<PagedResult<RecurringContractListItemDto>> ListAsync(RecurringContractListQuery query, CancellationToken cancellationToken = default);
    Task<RecurringContractDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<Guid>> CreateAsync(UpsertRecurringContractDto dto, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(Guid id, UpsertRecurringContractDto dto, CancellationToken cancellationToken = default);
    Task<Result> ActivateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> SuspendAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> ResumeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> CancelAsync(Guid id, DateTime? cancellationDate = null, CancellationToken cancellationToken = default);
    Task<Result> AmendAsync(Guid id, AmendRecurringContractDto dto, CancellationToken cancellationToken = default);
    Task<Result<Guid>> ConvertFromQuoteAsync(Guid quoteId, UpsertRecurringContractDto dto, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UsageMetricDto>> ListUsageMetricsAsync(CancellationToken cancellationToken = default);
    Task<Result<Guid>> CreateUsageMetricAsync(UpsertUsageMetricDto dto, CancellationToken cancellationToken = default);
    Task<Result> UpdateUsageMetricAsync(Guid id, UpsertUsageMetricDto dto, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UsageRecordDto>> ListUsageRecordsAsync(Guid contractId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<Result<Guid>> RecordUsageAsync(Guid contractId, RecordUsageDto dto, CancellationToken cancellationToken = default);
    Task<Result<int>> ImportUsageRecordsAsync(Guid contractId, IReadOnlyList<ImportUsageRecordRowDto> rows, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringContractBillingRunDto>> ListBillingRunsAsync(Guid contractId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PendingRecurringDraftDto>> ListPendingDraftsAsync(CancellationToken cancellationToken = default);
    Task<Result<int>> TriggerBillingAsync(Guid? contractId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Émet la facture FAC validée d'un run <c>DraftCreated</c> via <c>SubmitInvoiceCommand</c>
    /// (sans wizard). En cas d'échec, le run reste <c>DraftCreated</c> pour retry.
    /// </summary>
    Task<Result<InvoiceCreatedResultDto>> IssueBillingRunAsync(Guid billingRunId, CancellationToken cancellationToken = default);

    // ── Extensions v1 (ajouts — aucune signature existante modifiée) ──

    /// <summary>Clone un contrat en brouillon (en-tête + lignes actives, sans historique ni runs).</summary>
    Task<Result<Guid>> CloneAsync(Guid id, CloneRecurringContractDto dto, CancellationToken cancellationToken = default);
    /// <summary>Renouvelle un contrat actif/expiré d'une durée identique à la durée initiale ; retourne la nouvelle EndDate.</summary>
    Task<Result<DateTime>> RenewAsync(Guid id, RenewRecurringContractDto dto, CancellationToken cancellationToken = default);
    /// <summary>Notes éditables à tout statut (D15).</summary>
    Task<Result> UpdateNotesAsync(Guid id, string? notes, CancellationToken cancellationToken = default);

    /// <summary>Historique des avenants (null si le contrat n'existe pas).</summary>
    Task<IReadOnlyList<RecurringContractAmendmentDto>?> ListAmendmentsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RecurringContractAmendmentDetailDto?> GetAmendmentAsync(Guid id, Guid amendmentId, CancellationToken cancellationToken = default);

    /// <summary>Échéancier prévisionnel fusionné avec les runs existants (null si le contrat n'existe pas).</summary>
    Task<IReadOnlyList<RecurringContractScheduleItemDto>?> GetScheduleAsync(Guid id, int count, CancellationToken cancellationToken = default);
    /// <summary>Synthèse financière du contrat (règle D4 ; null si le contrat n'existe pas).</summary>
    Task<RecurringContractFinancialSummaryDto?> GetFinancialSummaryAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Factures liées au contrat, avoirs inclus (null si le contrat n'existe pas).</summary>
    Task<IReadOnlyList<RecurringContractLinkedInvoiceDto>?> GetLinkedInvoicesAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Série mensuelle des montants facturés (null si le contrat n'existe pas).</summary>
    Task<IReadOnlyList<RecurringContractEvolutionPointDto>?> GetEvolutionAsync(Guid id, int months, CancellationToken cancellationToken = default);
    /// <summary>Vue détail enrichie (null si le contrat n'existe pas).</summary>
    Task<RecurringContractDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>KPI de la page liste.</summary>
    Task<RecurringContractStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Charge le brouillon d'un run <c>DraftCreated</c> pour le modal d'ajustement (lignes + totaux).
    /// </summary>
    Task<Result<AdjustableRecurringDraftDto>> GetAdjustableDraftAsync(
        Guid billingRunId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Met à jour uniquement désignation / quantité / prix HT des lignes existantes du brouillon.
    /// Le run reste <c>DraftCreated</c> ; aucune émission ni écriture comptable.
    /// </summary>
    Task<Result<AdjustableRecurringDraftDto>> AdjustDraftLinesAsync(
        Guid billingRunId, AdjustRecurringDraftLinesRequest request, CancellationToken cancellationToken = default);
}
