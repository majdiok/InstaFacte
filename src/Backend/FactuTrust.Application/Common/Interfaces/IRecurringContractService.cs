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
}
