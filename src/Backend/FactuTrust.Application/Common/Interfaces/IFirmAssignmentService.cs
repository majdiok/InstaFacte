using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces;

public interface IFirmAssignmentService
{
    Task<IReadOnlyList<AccountingFirmDirectoryItemDto>> SearchDirectoryAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<FirmClientAssignmentDto?> GetCompanyCurrentAssignmentAsync(Guid companyTenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FirmClientAssignmentDto>> GetCompanyAssignmentHistoryAsync(Guid companyTenantId, CancellationToken cancellationToken = default);
    Task<Result<FirmClientAssignmentDto>> RequestAssignmentAsync(Guid companyTenantId, Guid requestedByUserId, RequestFirmAssignmentDto dto, CancellationToken cancellationToken = default);
    Task<Result> RevokeByCompanyAsync(Guid companyTenantId, Guid revokedByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FirmClientAssignmentDto>> GetIncomingInvitationsAsync(Guid firmTenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FirmClientDossierDto>> GetActiveClientsAsync(Guid firmTenantId, CancellationToken cancellationToken = default);
    Task<Result> AcceptAssignmentAsync(Guid firmTenantId, Guid assignmentId, Guid respondedByUserId, CancellationToken cancellationToken = default);
    Task<Result> RejectAssignmentAsync(Guid firmTenantId, Guid assignmentId, Guid respondedByUserId, CancellationToken cancellationToken = default);
    Task<Result> RevokeByFirmAsync(Guid firmTenantId, Guid assignmentId, Guid revokedByUserId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveAssignmentAsync(Guid firmTenantId, Guid companyTenantId, CancellationToken cancellationToken = default);
    Task<AccountingFirmProfileDto?> GetFirmProfileAsync(Guid firmTenantId, CancellationToken cancellationToken = default);
    Task<Result> UpdateFirmProfileAsync(Guid firmTenantId, UpdateAccountingFirmProfileDto dto, CancellationToken cancellationToken = default);
}

public interface IFirmDashboardService
{
    Task<FirmDashboardDto> GetDashboardAsync(Guid firmTenantId, CancellationToken cancellationToken = default);
}

public interface IFirmContextService
{
    Task<AuthResponseDto> SwitchToClientAsync(Guid userId, Guid homeTenantId, Guid clientTenantId, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> ClearContextAsync(Guid userId, Guid homeTenantId, CancellationToken cancellationToken = default);
    Task<FirmContextDto> GetCurrentContextAsync(System.Security.Claims.ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}

public interface IAccountingFirmsFeature
{
    bool IsEnabled { get; }
}

public interface ITenantAuthTokenService
{
    Task<AuthResponseDto> GenerateTokensAsync(
        Guid userId,
        Guid homeTenantId,
        Guid? contextTenantId = null,
        string? contextCompanyName = null,
        CancellationToken cancellationToken = default);
}
