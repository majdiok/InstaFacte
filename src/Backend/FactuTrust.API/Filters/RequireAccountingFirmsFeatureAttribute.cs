using FactuTrust.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FactuTrust.API.Filters;

/// <summary>
/// Returns 404 when the accounting firms feature flag is disabled.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireAccountingFirmsFeatureAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var feature = context.HttpContext.RequestServices.GetService<IAccountingFirmsFeature>();
        if (feature is null || !feature.IsEnabled)
        {
            context.Result = new NotFoundResult();
            return;
        }

        await next();
    }
}
