using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Authorization;

/// <summary>
/// Accès à la déclaration mensuelle : écriture réservée au cabinet délégué ;
/// la société ne voit que les déclarations soumises ou verrouillées.
/// </summary>
public static class VatDeclarationAccess
{
    public const string WriteDeniedMessage =
        "Seul le cabinet comptable, en mode dossier client, peut préparer et soumettre la déclaration mensuelle.";

    public const string NotSubmittedMessage =
        "Aucune déclaration soumise n'est disponible pour cette période.";

    public const string NotSubmittedErrorCode = "VatDeclaration.NotSubmitted";

    /// <summary>Statuts visibles pour un utilisateur société (hors cabinet délégué).</summary>
    public static bool IsVisibleToCompany(VatDeclarationStatus status) =>
        status is VatDeclarationStatus.Submitted or VatDeclarationStatus.Locked;

    public static Error NotSubmitted() =>
        new(NotSubmittedErrorCode, NotSubmittedMessage);
}
