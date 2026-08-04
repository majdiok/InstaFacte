using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

public interface IFirmLeaveService
{
    Task EnsureDefaultsAsync(Guid firmTenantId, int year, CancellationToken cancellationToken = default);

    Task<FirmLeaveOverviewDto> GetOverviewAsync(Guid firmTenantId, int year, bool isManager, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FirmLeaveCalendarEntryDto>> GetCalendarAsync(
        Guid firmTenantId, DateTime from, DateTime to, bool isManager, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FirmLeaveRequestDto>> ListRequestsAsync(
        Guid firmTenantId,
        bool isManager,
        Guid actorUserId,
        Guid? userId,
        int? status,
        Guid? typeId,
        DateTime? from,
        DateTime? to,
        int? year,
        CancellationToken cancellationToken = default);

    Task<FirmLeaveRequestDto?> GetRequestAsync(Guid firmTenantId, Guid id, bool isManager, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<Result<ComputeFirmLeaveDaysResultDto>> ComputeDaysAsync(Guid firmTenantId, int year, ComputeFirmLeaveDaysDto dto, CancellationToken cancellationToken = default);

    Task<Result<FirmLeaveRequestDto>> CreateAsync(
        Guid firmTenantId, Guid actorUserId, bool isManager, CreateFirmLeaveRequestDto dto, CancellationToken cancellationToken = default);

    Task<Result<FirmLeaveRequestDto>> UpdateAsync(
        Guid firmTenantId, Guid actorUserId, bool isManager, Guid id, UpdateFirmLeaveRequestDto dto, CancellationToken cancellationToken = default);

    Task<Result<FirmLeaveRequestDto>> SubmitAsync(
        Guid firmTenantId, Guid actorUserId, bool isManager, Guid id, CancellationToken cancellationToken = default);

    Task<Result<FirmLeaveRequestDto>> CancelAsync(
        Guid firmTenantId, Guid actorUserId, bool isManager, Guid id, CancellationToken cancellationToken = default);

    Task<Result<FirmLeaveRequestDto>> ProcessAsync(
        Guid firmTenantId, Guid processorUserId, string processorName, Guid id, ProcessFirmLeaveDto dto, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FirmLeaveBalanceDto>> ListBalancesAsync(
        Guid firmTenantId, int year, bool isManager, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<FirmLeaveBalanceDto?> GetMyBalanceAsync(Guid firmTenantId, Guid userId, int year, CancellationToken cancellationToken = default);

    Task<Result<FirmLeaveBalanceDto>> SetBalanceAsync(
        Guid firmTenantId, Guid userId, int year, SetFirmLeaveBalanceDto dto, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FirmLeaveTypeDto>> ListTypesAsync(Guid firmTenantId, bool activeOnly, CancellationToken cancellationToken = default);

    Task<Result<FirmLeaveTypeDto>> UpsertTypeAsync(Guid firmTenantId, UpsertFirmLeaveTypeDto dto, CancellationToken cancellationToken = default);

    Task<FirmLeaveSettingsDto> GetSettingsAsync(Guid firmTenantId, int year, CancellationToken cancellationToken = default);

    Task<Result<FirmLeaveSettingsDto>> UpdateSettingsAsync(Guid firmTenantId, int year, UpdateFirmLeaveSettingsDto dto, CancellationToken cancellationToken = default);

    Task<Result<(byte[] Content, string FileName)>> ExportListAsync(
        Guid firmTenantId, int? year, int? status, Guid? typeId, CancellationToken cancellationToken = default);

    Task<Result<(byte[] Content, string FileName)>> ExportSynthesisAsync(
        Guid firmTenantId, int year, CancellationToken cancellationToken = default);
}
