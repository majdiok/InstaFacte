using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Re-seals the audit hash chain in canonical order (CreatedAt, Id). Intended for one-off recovery after backup.
/// </summary>
public interface IAuditChainRepairService
{
    Task<Result<int>> ResealChainAsync(CancellationToken cancellationToken = default);
}
