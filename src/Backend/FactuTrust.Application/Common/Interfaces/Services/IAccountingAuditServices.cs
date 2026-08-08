using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IAccountingAuditQueryService
{
    Task<Result<AccountingAuditDashboardDto>> GetDashboardAsync(
        int fiscalYear,
        CancellationToken cancellationToken = default);

    Task<Result<PagedAnomaliesDto>> GetAnomaliesAsync(
        AccountingAnomalyFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<Result<AccountingAnomalyDetailDto>> GetAnomalyDetailAsync(
        Guid anomalyId,
        CancellationToken cancellationToken = default);

    Task<Result<AccountingAuditAnalyticsDto>> GetAnalyticsAsync(
        int fiscalYear,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AccountingControlModuleDto>>> GetModulesAsync(
        int fiscalYear,
        CancellationToken cancellationToken = default);
}

public interface IAccountingAuditWorkflowService
{
    Task<Result<AccountingAnomalyDetailDto>> AssignAsync(
        Guid anomalyId, Guid? assigneeUserId, string? assigneeUserName, Guid actorUserId, string? actorUserName,
        CancellationToken cancellationToken = default);

    Task<Result<AccountingAnomalyDetailDto>> UpdateStatusAsync(
        Guid anomalyId, int status, Guid actorUserId, string? actorUserName,
        CancellationToken cancellationToken = default);

    Task<Result<AccountingAnomalyDetailDto>> AddCommentAsync(
        Guid anomalyId, string comment, Guid actorUserId, string? actorUserName,
        CancellationToken cancellationToken = default);

    Task<Result<AccountingAnomalyDetailDto>> IgnoreAsync(
        Guid anomalyId, string reason, Guid actorUserId, string? actorUserName,
        CancellationToken cancellationToken = default);

    Task<Result<AccountingAnomalyDetailDto>> ResolveAsync(
        Guid anomalyId, Guid actorUserId, string? actorUserName,
        CancellationToken cancellationToken = default);
}

public interface IAccountingAuditExportService
{
    Task<Result<byte[]>> ExportCsvAsync(AccountingAnomalyFilterDto filter, CancellationToken cancellationToken = default);
    Task<Result<byte[]>> ExportPdfAsync(int fiscalYear, CancellationToken cancellationToken = default);
}

public interface IAccountingAuditScheduleService
{
    Task<Result<IReadOnlyList<AccountingControlScheduleDto>>> ListSchedulesAsync(CancellationToken cancellationToken = default);
    Task<Result<AccountingControlScheduleDto>> SaveScheduleAsync(AccountingControlScheduleDto dto, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteScheduleAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IAccountingAuditRuleSettingsService
{
    Task<Result<IReadOnlyList<AccountingControlRuleSettingDto>>> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<AccountingControlRuleSettingDto>>> SaveSettingsAsync(
        IReadOnlyList<AccountingControlRuleSettingDto> settings,
        CancellationToken cancellationToken = default);
}
