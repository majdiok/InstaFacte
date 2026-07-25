using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common;

public static class FirmDossierAccessScopeExtensions
{
    public static bool TryGetAccessScope(this ICurrentUser currentUser, out FirmDossierAccessScope scope)
    {
        if (currentUser.UserId is Guid userId && currentUser.Role is UserRole role
            && (role == UserRole.FirmManager || role == UserRole.FirmAccountant))
        {
            scope = FirmDossierAccessScope.ForUser(userId, role);
            return true;
        }

        scope = default;
        return false;
    }
}
