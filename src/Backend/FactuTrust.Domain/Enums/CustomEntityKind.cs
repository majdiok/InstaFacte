namespace FactuTrust.Domain.Enums;

/// <summary>
/// Nature d'une entité Studio (<c>CustomEntityDefinition</c>). Persistée en <c>int</c>
/// (colonne <c>Kind</c>, défaut 0) ; jamais modifiable après création.
/// </summary>
public enum CustomEntityKind
{
    /// <summary>Table métier ordinaire, visible dans la navigation.</summary>
    Standard = 0,

    /// <summary>
    /// Table de jonction d'une relation plusieurs‑à‑plusieurs : deux champs
    /// <c>RelationCustom</c> requis (source, cible), unicité de paire, exclue de la navigation.
    /// </summary>
    Junction = 1
}
