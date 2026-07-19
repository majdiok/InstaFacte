using FactuTrust.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace FactuTrust.API.Authorization;

public sealed class FirmDelegatedContextAuthorizationHandler
    : AuthorizationHandler<FirmDelegatedContextRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        FirmDelegatedContextRequirement requirement)
    {
        if (AccountingValidationAccess.IsAccountingFirmDelegatedContext(context.User))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
