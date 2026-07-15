using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>Resolves the authenticated tenant + user for Studio handlers, failing fast otherwise.</summary>
public static class StudioContext
{
    public static bool TryGet(ICurrentUser currentUser, out Guid tenantId, out Guid? userId, out Error error)
    {
        tenantId = Guid.Empty;
        userId = currentUser.UserId;
        error = Error.None;

        var tid = currentUser.TenantId;
        if (tid is null || tid == Guid.Empty)
        {
            error = Error.Unauthorized("Aucun tenant authentifié.");
            return false;
        }

        tenantId = tid.Value;
        return true;
    }
}
