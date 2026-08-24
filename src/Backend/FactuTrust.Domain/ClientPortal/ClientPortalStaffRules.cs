using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.ClientPortal;

/// <summary>
/// Staff-user screens must never assign <see cref="UserRole.Client"/>.
/// Portal contacts are invited from the client card only.
/// </summary>
public static class ClientPortalStaffRules
{
    public const string InviteFromClientCardMessage =
        "Les accès portail s'invitent depuis la fiche client (onglet Espace client).";

    public static bool IsStaffAssignable(UserRole role) => role != UserRole.Client;

    public static bool IsAssignableByStaff(UserRole role) => IsStaffAssignable(role);
}
