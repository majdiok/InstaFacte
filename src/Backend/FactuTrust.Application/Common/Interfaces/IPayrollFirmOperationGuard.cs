namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Guards firm-exclusive payroll operations when a cabinet assignment is active.
/// </summary>
public interface IPayrollFirmOperationGuard
{
    Task<bool> RequiresFirmExclusiveExecutionAsync(CancellationToken cancellationToken = default);

    Task<bool> CanExecuteFirmExclusiveOperationsAsync(CancellationToken cancellationToken = default);
}
