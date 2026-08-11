using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Domain.Authorization;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services;

public sealed class PayrollFirmOperationGuard : IPayrollFirmOperationGuard
{
    private readonly ITenantContext _tenantContext;
    private readonly IFirmAssignmentService _firmAssignmentService;
    private readonly ICurrentUser _currentUser;
    private readonly PayrollOptions _payrollOptions;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private bool _loaded;
    private bool _requiresFirmExclusiveExecution;
    private bool _canExecuteFirmExclusiveOperations;

    public PayrollFirmOperationGuard(
        ITenantContext tenantContext,
        IFirmAssignmentService firmAssignmentService,
        ICurrentUser currentUser,
        IOptions<PayrollOptions> payrollOptions)
    {
        _tenantContext = tenantContext;
        _firmAssignmentService = firmAssignmentService;
        _currentUser = currentUser;
        _payrollOptions = payrollOptions.Value;
    }

    public async Task<bool> RequiresFirmExclusiveExecutionAsync(CancellationToken cancellationToken = default)
    {
        await LoadAsync(cancellationToken);
        return _requiresFirmExclusiveExecution;
    }

    public async Task<bool> CanExecuteFirmExclusiveOperationsAsync(CancellationToken cancellationToken = default)
    {
        await LoadAsync(cancellationToken);
        return _canExecuteFirmExclusiveOperations;
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
            return;

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_loaded)
                return;

            var hasActiveFirmAssignment = false;
            if (_payrollOptions.FirmExclusiveOperations)
            {
                var companyTenantId = _tenantContext.TenantId;
                if (companyTenantId is not null && companyTenantId != Guid.Empty)
                {
                    var assignment = await _firmAssignmentService.GetCompanyCurrentAssignmentAsync(
                        companyTenantId.Value,
                        cancellationToken);
                    hasActiveFirmAssignment = assignment is not null;
                }
            }

            _requiresFirmExclusiveExecution = hasActiveFirmAssignment;
            _canExecuteFirmExclusiveOperations = PayrollOperationsAccess.CanExecuteFirmExclusiveOperations(
                hasActiveFirmAssignment,
                _currentUser.IsAccountingFirmDelegatedContext);
            _loaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }
}
