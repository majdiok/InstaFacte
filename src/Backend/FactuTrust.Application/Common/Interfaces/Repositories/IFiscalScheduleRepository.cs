using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public sealed record FiscalScheduleQueryCriteria
{
    public int? FiscalYear { get; init; }
    public int? PeriodMonth { get; init; }
    public int? PeriodQuarter { get; init; }
    public FiscalObligationType? ObligationType { get; init; }
    public FiscalScheduleStatus? Status { get; init; }
    public Guid? ResponsibleUserId { get; init; }
    public DateTime? DueFrom { get; init; }
    public DateTime? DueTo { get; init; }
    public string? Search { get; init; }
    public bool IncludeCancelled { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed record FiscalScheduleQueryResult(
    IReadOnlyList<FiscalScheduleEntry> Items,
    IReadOnlyList<FiscalScheduleEntry> SummaryItems,
    int TotalCount);

public interface IFiscalScheduleRepository
{
    Task<FiscalScheduleQueryResult> ListAsync(FiscalScheduleQueryCriteria criteria, CancellationToken cancellationToken = default);

    Task<FiscalScheduleEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        FiscalObligationType obligationType,
        int fiscalYear,
        int? periodMonth,
        int? periodQuarter,
        FiscalScheduleSourceType sourceType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Échéance « Déclaration mensuelle » NON annulée d'une période (entité trackée, prête à
    /// être mutée puis passée à <see cref="UpdateAsync"/>) — utilisée par la synchronisation
    /// déclaration mensuelle → échéancier. Null si l'exercice n'a pas été généré.
    /// </summary>
    Task<FiscalScheduleEntry?> GetMonthlyDeclarationEntryAsync(
        int fiscalYear, int periodMonth, CancellationToken cancellationToken = default);

    Task AddAsync(FiscalScheduleEntry entry, FiscalScheduleHistoryEntry? history = null, CancellationToken cancellationToken = default);

    Task UpdateAsync(FiscalScheduleEntry entry, FiscalScheduleHistoryEntry? history = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FiscalScheduleHistoryEntry>> GetHistoryAsync(Guid fiscalScheduleEntryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FiscalScheduleAttachment>> GetAttachmentsAsync(Guid fiscalScheduleEntryId, CancellationToken cancellationToken = default);

    Task<FiscalScheduleAttachment?> GetAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    Task AddAttachmentAsync(FiscalScheduleAttachment attachment, FiscalScheduleHistoryEntry? history = null, CancellationToken cancellationToken = default);

    Task DeleteAttachmentAsync(FiscalScheduleAttachment attachment, FiscalScheduleHistoryEntry? history = null, CancellationToken cancellationToken = default);
}
