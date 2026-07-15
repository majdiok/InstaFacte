namespace FactuTrust.Domain.Enums;

/// <summary>
/// Cycle de vie d'une écriture comptable (brouillard → validation → clôture).
/// </summary>
/// <remarks>
/// Rétro-compatibilité : les écritures existantes (avant l'introduction du brouillard) sont
/// migrées en <see cref="Validee"/> afin que les états (balance, grand livre, bilan, résultat)
/// restent strictement identiques. La création d'écriture défaut sur <see cref="Validee"/> tant
/// que le workflow brouillard n'est pas activé pour le dossier.
/// </remarks>
public enum JournalEntryStatus
{
    /// <summary>Écriture provisoire, modifiable/supprimable, en attente de validation.</summary>
    Brouillon = 0,

    /// <summary>Écriture validée : définitive, immuable, corrigeable uniquement par extourne.</summary>
    Validee = 1,

    /// <summary>Écriture d'une période clôturée : verrouillée, aucune action possible.</summary>
    Cloturee = 2
}
