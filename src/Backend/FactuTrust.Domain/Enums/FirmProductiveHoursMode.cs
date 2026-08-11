namespace FactuTrust.Domain.Enums;

/// <summary>
/// Façon de déterminer les heures productives annuelles, dénominateur du taux horaire de revient.
/// </summary>
/// <remarks>
/// <para>
/// Le mode existe pour la non-régression : un exercice déjà analysé ne doit pas voir ses taux
/// changer sous l'effet d'une mise à jour. Les exercices existants restent en
/// <see cref="Parametric"/> ; l'individualisation est adoptée exercice par exercice, en connaissance
/// de cause.
/// </para>
/// </remarks>
public enum FirmProductiveHoursMode
{
    /// <summary>
    /// Forfait identique pour tous : congés et jours fériés déduits d'après les paramètres de
    /// l'exercice. Taux stable et comparable entre collaborateurs comme entre exercices.
    /// </summary>
    Parametric = 0,

    /// <summary>
    /// Congés réellement approuvés du collaborateur, et prorata de sa présence sur l'exercice.
    /// Plus juste économiquement, mais le taux évolue en cours d'année à mesure des absences.
    /// </summary>
    IndividualRealLeaves = 1
}
