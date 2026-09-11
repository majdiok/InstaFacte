using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

public interface ITimesheetService
{
    Task<TenantTimesheetSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<Result> UpdateSettingsAsync(UpdateTenantTimesheetSettingsDto dto, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeBillingTimeTargetDto>> ListTargetsAsync(int year, int month, CancellationToken cancellationToken = default);
    Task<Result> UpsertTargetAsync(UpsertEmployeeBillingTimeTargetDto dto, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimesheetTipDto>> ListTipsAsync(CancellationToken cancellationToken = default);
    Task<Result<Guid>> CreateTipAsync(UpsertTimesheetTipDto dto, CancellationToken cancellationToken = default);
    Task<Result> UpdateTipAsync(Guid id, UpsertTimesheetTipDto dto, CancellationToken cancellationToken = default);
    Task<Result> DeleteTipAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TimesheetBillingRateKpiDto> GetBillingRateKpiAsync(int year, int month, Guid? userId, CancellationToken cancellationToken = default);
    Task<TimesheetLeaderboardDto> GetLeaderboardAsync(int year, int month, string mode, CancellationToken cancellationToken = default);
    Task<TimesheetGridDto> GetGridAsync(TimesheetGridQuery query, CancellationToken cancellationToken = default);

    Task<Result<TimesheetTimerStateDto>> StartTimerAsync(StartTimesheetTimerDto dto, CancellationToken cancellationToken = default);
    Task<Result<decimal>> StopTimerAsync(Guid entryId, CancellationToken cancellationToken = default);
    Task<TimesheetTimerStateDto?> GetActiveTimerAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeOffRequestDto>> ListTimeOffRequestsAsync(CancellationToken cancellationToken = default);
    Task<Result<Guid>> CreateTimeOffRequestAsync(CreateTimeOffRequestDto dto, CancellationToken cancellationToken = default);
    Task<Result> ApproveTimeOffRequestAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> RefuseTimeOffRequestAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectTimeEntryDto>> ListPendingValidationAsync(CancellationToken cancellationToken = default);
}
