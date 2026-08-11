namespace FactuTrust.Domain.Enums;

/// <summary>
/// Sort du report d'un congé cabinet vers la paie interne du cabinet.
/// </summary>
/// <remarks>
/// <para>
/// Les congés du cabinet sont la source unique : c'est leur approbation qui doit produire la
/// retenue pour absence, l'indemnité journalière CNSS et l'acquisition de droits. Le report vers
/// la base de paie peut néanmoins échouer ou être refusé, sans que l'acte RH doive être annulé
/// pour autant — d'où cet état, porté par la demande elle-même et rattrapable au rapprochement.
/// </para>
/// </remarks>
public enum FirmLeavePayrollMirrorState
{
    /// <summary>Pas encore reporté : demande non approuvée, ou report jamais tenté.</summary>
    NotMirrored = 0,

    /// <summary>Reporté : un congé de paie correspondant existe.</summary>
    Mirrored = 1,

    /// <summary>Le type d'absence n'a aucun effet paie (télétravail, formation…).</summary>
    NoPayrollEffect = 2,

    /// <summary>
    /// La paie du mois concerné est arrêtée : les bulletins sont gelés, rien n'a été écrit.
    /// </summary>
    BlockedFrozenPayroll = 3,

    /// <summary>Le report a échoué (base de paie injoignable, salarié non lié…).</summary>
    Failed = 4,

    /// <summary>Le congé de paie a été retiré après coup (demande annulée ou rejouée).</summary>
    Revoked = 5
}
