using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IAccountingAuditEngine
{
    Task<Result<AccountingAuditRunResultDto>> RunAsync(
        AccountingAuditRunRequestDto request,
        Guid? userId,
        string? userName,
        CancellationToken cancellationToken = default);

    Task<Result<AccountingAuditRunStatusDto>> GetRunStatusAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}
