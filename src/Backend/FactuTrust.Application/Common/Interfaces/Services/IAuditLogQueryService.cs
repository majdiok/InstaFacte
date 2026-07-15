using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IAuditLogQueryService
{
    Task<Result<PagedResult<AuditLogEntryDto>>> GetLogsAsync(
        DateTime? from,
        DateTime? to,
        string? action,
        Guid? userId,
        string? entityType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<AuditLogDetailDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<AuditChainVerificationDto>> VerifyChainAsync(CancellationToken cancellationToken = default);
}
