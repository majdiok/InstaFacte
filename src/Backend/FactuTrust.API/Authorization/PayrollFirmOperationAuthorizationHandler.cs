using FactuTrust.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace FactuTrust.API.Authorization;

public sealed class PayrollFirmOperationAuthorizationHandler
    : AuthorizationHandler<PayrollFirmOperationRequirement>
{
    private readonly IPayrollFirmOperationGuard _guard;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PayrollFirmOperationAuthorizationHandler(
        IPayrollFirmOperationGuard guard,
        IHttpContextAccessor httpContextAccessor)
    {
        _guard = guard;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PayrollFirmOperationRequirement requirement)
    {
        var cancellationToken = _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        if (await _guard.CanExecuteFirmExclusiveOperationsAsync(cancellationToken))
            context.Succeed(requirement);
    }
}
