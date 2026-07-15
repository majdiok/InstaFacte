using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IAccountingPeriodService
{
    Task<Result<AccountingPeriod>> EnsureOpenPeriodAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes a period using a serializable transaction to prevent concurrent entry creation.
    /// Refuses to close while draft (brouillon) entries remain in the period, then marks the
    /// period's validated entries as <c>Cloturee</c>.
    /// </summary>
    Task<Result> ClosePeriodWithLockAsync(Guid periodId, string closedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reopens a closed period and reverts its <c>Cloturee</c> entries back to <c>Validee</c>
    /// (symmetric counterpart of <see cref="ClosePeriodWithLockAsync"/>). Idempotent if the
    /// period is already open.
    /// </summary>
    Task<Result<AccountingPeriod>> ReopenPeriodAsync(Guid periodId, CancellationToken cancellationToken = default);
}
