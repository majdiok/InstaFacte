using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface ITejXmlExportLogRepository
{
    Task AddAsync(TejXmlExportLog log, CancellationToken ct = default);
    Task<List<TejXmlExportLog>> GetRecentAsync(int take = 50, CancellationToken ct = default);
}
