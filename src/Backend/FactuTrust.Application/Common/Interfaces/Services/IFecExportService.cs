using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IFecExportService
{
    Task<Result<byte[]>> ExportFecAsync(int fiscalYear, CancellationToken cancellationToken = default);
}
