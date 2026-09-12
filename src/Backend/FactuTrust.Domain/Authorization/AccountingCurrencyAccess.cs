using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Authorization;

/// <summary>
/// Permissions gouvernées par le drapeau <c>Accounting:MultiCurrencyEnabled</c>.
///
/// <para>
/// <b>Pourquoi ce filtre existe.</b> Sans lui, <c>accounting:currencies_manage</c> entre dans le JWT
/// quel que soit l'état du module : l'autorisation HTTP passe, l'interface affiche le bouton
/// « Ajouter une devise », et c'est le handler qui refuse en 400. L'utilisateur se voit proposer une
/// action que le serveur refusera toujours. En retirant la clé à l'émission, le menu et les boutons
/// disparaissent d'eux-mêmes : la permission porte à la fois le droit <i>et</i> l'état du module.
/// </para>
///
/// <para>
/// C'est la doctrine déjà appliquée par <c>FirmGovernanceNativeAccess.BuildFirmRevisionPermissions</c>
/// pour <c>FirmRevisionEnabled</c>. Celle-ci ne couvre toutefois que le cabinet natif ; le filtre est
/// ici appliqué après l'aiguillage des populations, donc aussi au mode délégué et aux sociétés.
/// </para>
///
/// <para>
/// <b>Ce filtre ne remplace pas les garde-fous des handlers.</b> Un JWT vit quinze minutes et
/// survit à une bascule du drapeau : le serveur doit continuer de refuser l'écriture pendant cette
/// fenêtre. Les deux mécanismes sont complémentaires.
/// </para>
/// </summary>
public static class AccountingCurrencyAccess
{
    private static readonly HashSet<string> FlagGatedPermissions = new(StringComparer.Ordinal)
    {
        Permissions.Accounting.CurrenciesManage,
        Permissions.Accounting.ExchangeRateOverride
    };

    public static IReadOnlyCollection<string> FlagGatedPermissionList => FlagGatedPermissions;

    public static bool IsMultiCurrencyPermission(string permission) =>
        FlagGatedPermissions.Contains(permission);

    /// <summary>
    /// Retire les permissions multi-devises lorsque le module est éteint. Les autres clés sont
    /// rendues intactes, dans leur ordre d'origine.
    /// </summary>
    public static IReadOnlyList<string> FilterWhenDisabled(
        IEnumerable<string> permissions,
        bool multiCurrencyEnabled) =>
        multiCurrencyEnabled
            ? permissions.ToList()
            : permissions.Where(p => !IsMultiCurrencyPermission(p)).ToList();
}
