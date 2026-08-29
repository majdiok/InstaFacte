namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Résolveur déterministe d'exercice comptable (plan « Exercices décalés », décision D2).
/// Toutes les méthodes sont pures : elles ne dépendent que des paramètres — aucune lecture de
/// configuration. Le mois de début d'exercice (<c>fiscalYearStartMonth</c>) est fourni par
/// l'appelant (récupéré depuis <c>FixedAssetSettings</c>) ; 1 = exercice civil.
/// </summary>
/// <remarks>
/// Stabilité du contrat : ce résolveur est consommé par P2 (moteur), P3 (run/écritures) et par
/// P4 (exports/rapports) et le frontend (via les DTO). Les signatures ci-dessous sont figées.
/// </remarks>
public interface IFiscalYearResolver
{
    /// <summary>
    /// Clé logique d'exercice (int = année de début d'exercice) contenant la date donnée.
    /// Pour un exercice démarrant en mois <paramref name="fiscalYearStartMonth"/> :
    /// date de mois ≥ startMonth → année de la date ; sinon → année précédente.
    /// Avec <paramref name="fiscalYearStartMonth"/> = 1, retourne toujours <c>date.Year</c>
    /// (exercice civil = année civile, comportement historique préservé).
    /// </summary>
    int FiscalYearKey(DateTime date, int fiscalYearStartMonth);

    /// <summary>Date de début d'exercice (premier jour du mois de début), pour la clé donnée.</summary>
    DateTime FiscalYearStartDateTime(int fiscalYearKey, int fiscalYearStartMonth);

    /// <summary>
    /// Date de fin d'exercice = dernier jour du mois précédant le mois de début de l'exercice
    /// suivant (gère correctement les années bissextiles : 28/29 février). Pour un exercice
    /// civil (mois 1), retourne le 31/12 de l'année de la clé.
    /// </summary>
    DateTime FiscalYearEndDateTime(int fiscalYearKey, int fiscalYearStartMonth);

    /// <summary>
    /// Libellé d'affichage de l'exercice. Exercice civil (mois 1) → « N » (ex. « 2026 »).
    /// Exercice décalé : <c>"N/N+1"</c> → « 2026/2027 » ; <c>"N"</c> → « 2026 »
    /// (décision D2). Toute autre valeur de format est traitée comme « N/N+1 ».
    /// </summary>
    string FiscalYearLabel(int fiscalYearKey, int fiscalYearStartMonth, string labelFormat);
}
