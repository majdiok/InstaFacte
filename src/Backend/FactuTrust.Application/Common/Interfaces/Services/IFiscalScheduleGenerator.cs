using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IFiscalScheduleGenerator
{
    Task<Result<int>> EnsureFiscalYearAsync(int fiscalYear, CancellationToken cancellationToken = default);
}
